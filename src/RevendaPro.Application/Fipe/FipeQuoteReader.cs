using RevendaPro.Domain.Entities;
using RevendaPro.Domain.Interfaces;
using RevendaPro.Domain.Interfaces.Reference;
using RevendaPro.Domain.ValueObjects;

namespace RevendaPro.Application.Fipe
{
    /// <summary>
    /// Reads the reference table through what is already kept.
    /// </summary>
    public interface IFipeQuoteReader
    {
        /// <summary>
        /// The quote of one model in the table published now.
        ///
        /// The caller commits: a quote that was fetched is enqueued like any other write, so
        /// it lands together with whatever the operation was doing.
        /// </summary>
        /// <param name="fipeCode">Code of the model in the table.</param>
        /// <param name="yearFuel">Year and fuel of the priced row.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The quote, or why there is none.</returns>
        Task<FipeResult<FipeQuote>> GetCurrentAsync(
            string fipeCode,
            string yearFuel,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Finds the year-fuel pair of a model from the model year alone.
        ///
        /// A car registered before this milestone carries the code and no pair, and the pair
        /// is what the table prices. When one single row of that model matches the year, it
        /// is the answer; two rows mean the same year exists as flex and as petrol, and that
        /// is a question for a person.
        /// </summary>
        /// <param name="fipeCode">Code of the model in the table.</param>
        /// <param name="modelYear">Model year of the vehicle.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The single matching row, or why there is none.</returns>
        Task<FipeResult<FipeYearOption>> ResolveYearFuelAsync(
            string fipeCode,
            short modelYear,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// The kept quote first, the source only when there is none.
    ///
    /// This is what makes the automatic lookup affordable. The table changes once a month and
    /// the yard holds dozens of cars, so ten cars of the same model and year are <b>one</b>
    /// call — and a month already fetched never goes back to the network, because a quote of
    /// a closed month is a historical fact and never changes. See ADR-0005.
    ///
    /// It remembers within its own scope, which is a request or one run of the monthly
    /// routine. Two cars of the same model in the same run resolve the published table once
    /// and the price once, even before the write reaches the database. Across requests the
    /// kept row does the remembering, which is the durable half of the same promise.
    ///
    /// Nothing here throws for a source problem: the reading carries the outcome, and the
    /// caller decides what to do with a table that is out of reach.
    /// </summary>
    public class FipeQuoteReader(IUnitOfWork unitOfWork, IFipeCatalog catalog) : IFipeQuoteReader
    {
        private readonly Dictionary<string, FipeQuote> resolved =
            new(StringComparer.OrdinalIgnoreCase);

        private FipeReference? published;

        /// <inheritdoc/>
        public async Task<FipeResult<FipeQuote>> GetCurrentAsync(
            string fipeCode,
            string yearFuel,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(fipeCode);
            ArgumentException.ThrowIfNullOrWhiteSpace(yearFuel);

            var code = fipeCode.Trim();
            var year = yearFuel.Trim();

            var table = await PublishedTableAsync(cancellationToken).ConfigureAwait(false);

            if (!table.Ok)
            {
                return new FipeResult<FipeQuote>(table.Outcome, null, table.Detail);
            }

            var kept = await KeptAsync(code, year, table.Value!.Month, cancellationToken)
                .ConfigureAwait(false);

            if (kept is not null)
            {
                return FipeResult<FipeQuote>.Found(kept);
            }

            // Pinned to the table resolved above, and never left to "the current one": the
            // same source answered two different months for the same car inside a minute.
            var price = await catalog
                .GetPriceAsync(code, year, table.Value.Code, cancellationToken)
                .ConfigureAwait(false);

            if (!price.Ok)
            {
                return new FipeResult<FipeQuote>(price.Outcome, null, price.Detail);
            }

            // The month kept is the one the answer carried. When it differs from the one
            // asked for — which is the mirror contradicting itself, and it has happened —
            // that month may already be kept, and writing it again would break the one row
            // per model and month this whole design rests on.
            var answered = price.Value!.Reference;

            if (answered != table.Value.Month)
            {
                kept = await KeptAsync(code, year, answered, cancellationToken)
                    .ConfigureAwait(false);

                if (kept is not null)
                {
                    return FipeResult<FipeQuote>.Found(kept);
                }
            }

            var quote = FipeQuote.Create(price.Value);

            unitOfWork.FipeQuoteRepository.Add(quote);

            return FipeResult<FipeQuote>.Found(Remember(quote));
        }

        /// <inheritdoc/>
        public async Task<FipeResult<FipeYearOption>> ResolveYearFuelAsync(
            string fipeCode,
            short modelYear,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(fipeCode);

            var table = await PublishedTableAsync(cancellationToken).ConfigureAwait(false);

            if (!table.Ok)
            {
                return new FipeResult<FipeYearOption>(table.Outcome, null, table.Detail);
            }

            var years = await catalog
                .ListYearsAsync(fipeCode.Trim(), table.Value!.Code, cancellationToken)
                .ConfigureAwait(false);

            if (!years.Ok)
            {
                return new FipeResult<FipeYearOption>(years.Outcome, null, years.Detail);
            }

            var matching = years.Value!.Where(option => option.ModelYear == modelYear).ToList();

            return matching.Count switch
            {
                1 => FipeResult<FipeYearOption>.Found(matching[0]),

                // Zero is a fact about the car: the table prices other years of this model,
                // and this one is outside it.
                0 => FipeResult<FipeYearOption>.Missing(
                    $"A tabela lista {years.Value.Count} ano(s) deste modelo, e {modelYear} fica fora."),

                // More than one is the same year as flex and as petrol, at different prices.
                // Guessing here would put a number on the sheet that belongs to another car.
                _ => FipeResult<FipeYearOption>.Missing(
                    $"O ano {modelYear} deste modelo tem {matching.Count} versões na tabela.")
            };
        }

        /// <summary>
        /// Which table is published, asked once per scope. It changes once a month, and one
        /// run of the yard would otherwise ask the same question for every car.
        /// </summary>
        private async Task<FipeResult<FipeReference>> PublishedTableAsync(
            CancellationToken cancellationToken)
        {
            if (published is not null)
            {
                return FipeResult<FipeReference>.Found(published);
            }

            var read = await catalog.GetCurrentReferenceAsync(cancellationToken).ConfigureAwait(false);

            if (read.Ok)
            {
                published = read.Value;
            }

            return read;
        }

        /// <summary>What is already known for a model in a month, from this scope or the table.</summary>
        private async Task<FipeQuote?> KeptAsync(
            string fipeCode,
            string yearFuel,
            DateOnly month,
            CancellationToken cancellationToken)
        {
            if (resolved.TryGetValue(KeyOf(fipeCode, yearFuel, month), out var remembered))
            {
                return remembered;
            }

            var stored = await unitOfWork.FipeQuoteRepository
                .FindAsync(fipeCode, yearFuel, month, cancellationToken)
                .ConfigureAwait(false);

            return stored is null ? null : Remember(stored);
        }

        private FipeQuote Remember(FipeQuote quote)
        {
            resolved[KeyOf(quote.FipeCode, quote.YearFuel, quote.ReferenceMonth)] = quote;

            return quote;
        }

        private static string KeyOf(string fipeCode, string yearFuel, DateOnly month) =>
            $"{fipeCode}|{yearFuel}|{month:yyyy-MM}";
    }
}
