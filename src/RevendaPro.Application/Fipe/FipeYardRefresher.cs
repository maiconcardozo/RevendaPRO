using Foundation.Domain.Abstractions;
using RevendaPro.Domain.Entities;
using RevendaPro.Domain.Enums;
using RevendaPro.Domain.Interfaces;
using RevendaPro.Domain.ValueObjects;

namespace RevendaPro.Application.Fipe
{
    /// <summary>
    /// One run over the yard: every car still on the lot gets the table of this month.
    /// </summary>
    public interface IFipeYardRefresher
    {
        /// <summary>Looks at the yard once, and updates what is behind.</summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>What the run found and did.</returns>
        Task<FipeYardRun> RefreshAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// What one run over the yard found and did.
    /// </summary>
    /// <param name="PublishedMonth">The table that was published, or null when it was out of reach.</param>
    /// <param name="Looked">Cars behind that month.</param>
    /// <param name="Updated">Cars whose reference was written.</param>
    /// <param name="LeftAlone">Cars skipped because the value was typed by a person.</param>
    /// <param name="OutsideTheTable">Cars the table has no answer for.</param>
    /// <param name="Queries">Calls the run made for prices — one per model, and never per car.</param>
    public sealed record FipeYardRun(
        DateOnly? PublishedMonth,
        int Looked,
        int Updated,
        int LeftAlone,
        int OutsideTheTable,
        int Queries)
    {
        /// <summary>A run that ended before it started, because the table stayed quiet.</summary>
        public static FipeYardRun Quiet() => new(null, 0, 0, 0, 0, 0);
    }

    /// <summary>
    /// The yard, updated without anybody asking (RF-14).
    ///
    /// A car sitting on the lot loses reference value every month — around R$ 285 a month on
    /// the Cruze of the survey — and until now nobody could say how much. This is what keeps
    /// the number on every sheet current, so the screen of the M11 has something true to
    /// measure. See ADR-0005.
    ///
    /// <b>It runs in one scope, on purpose.</b> The reader resolves the published table once
    /// per scope and remembers every quote it fetched, so ten cars of the same model cost one
    /// call. A scope per car would double the calls and buy nothing.
    ///
    /// <b>It leaves a typed value alone.</b> A rare, imported or off-table car is priced by
    /// somebody who knows that market, and the table would replace that judgement with a
    /// number it never had. A person pressing the button is a different thing, and that one
    /// always goes through.
    /// </summary>
    public class FipeYardRefresher(IUnitOfWork unitOfWork, IFipeQuoteReader quotes)
        : IFipeYardRefresher
    {
        /// <summary>
        /// Most cars one run touches. It exists so a yard that grew, or a source that started
        /// failing halfway, cannot turn one run into hundreds of calls.
        /// </summary>
        private const int MostPerRun = 200;

        /// <inheritdoc/>
        public async Task<FipeYardRun> RefreshAsync(CancellationToken cancellationToken = default)
        {
            var table = await quotes.PublishedTableAsync(cancellationToken).ConfigureAwait(false);

            if (!table.Ok)
            {
                // The source is out of reach. Every sheet keeps the value it had, marked as
                // old, and the next run tries again.
                return FipeYardRun.Quiet();
            }

            var month = table.Value!.Month;

            var behind = await unitOfWork.VehicleRepository
                .ListBehindFipeAsync(month, MostPerRun, cancellationToken)
                .ConfigureAwait(false);

            var updated = 0;
            var leftAlone = 0;
            var outside = 0;

            foreach (var vehicle in behind)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!vehicle.AcceptsAutomaticFipe)
                {
                    leftAlone++;
                    continue;
                }

                var yearFuel = await YearFuelOfAsync(vehicle, cancellationToken).ConfigureAwait(false);

                if (yearFuel is null)
                {
                    outside++;
                    continue;
                }

                var quote = await quotes
                    .GetCurrentAsync(vehicle.FipeCode!, yearFuel, cancellationToken)
                    .ConfigureAwait(false);

                if (!quote.Ok)
                {
                    if (quote.Outcome == FipeOutcome.Missing)
                    {
                        outside++;
                        continue;
                    }

                    // The source stopped answering mid-run. Whatever was already written stays
                    // written; the rest waits for the next run rather than hammering a table
                    // that is already struggling.
                    break;
                }

                // A month that never moved is a month already on the sheet: the source can
                // answer an older table than the one it published, and writing it again would
                // be an update that changes nothing.
                if (quote.Value!.ReferenceMonth == vehicle.FipeReferenceDate
                    && quote.Value.Value == vehicle.FipeValue)
                {
                    continue;
                }

                vehicle.ApplyFipeReference(
                    quote.Value.Value,
                    quote.Value.ReferenceMonth,
                    quote.Value.FipeCode,
                    quote.Value.YearFuel,
                    Entity.SystemActor);

                unitOfWork.VehicleRepository.Update(vehicle);
                updated++;
            }

            if (updated > 0 || quotes.Queries > 0)
            {
                // One commit for the whole run: the vehicles and the quotes the reader
                // enqueued land together.
                await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
            }

            // The count of calls is the evidence that the quote table is doing its job: a run
            // over ten cars of the same model shows one.
            return new FipeYardRun(month, behind.Count, updated, leftAlone, outside, quotes.Queries);
        }

        /// <summary>
        /// The year-fuel pair of the vehicle, found from the model year when it is missing.
        /// </summary>
        /// <param name="vehicle">The vehicle.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The pair, or null when the table has no single row for that year.</returns>
        private async Task<string?> YearFuelOfAsync(Vehicle vehicle, CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(vehicle.FipeYearFuel))
            {
                return vehicle.FipeYearFuel;
            }

            var found = await quotes
                .ResolveYearFuelAsync(vehicle.FipeCode!, vehicle.ModelYear, cancellationToken)
                .ConfigureAwait(false);

            return found.Ok ? found.Value!.YearFuel : null;
        }
    }
}
