using MediatR;
using RevendaPro.Application.Fipe;
using RevendaPro.Application.Vehicles.Commands;
using RevendaPro.Application.Vehicles.DTOs;
using RevendaPro.Domain.Entities;
using RevendaPro.Domain.Enums;
using RevendaPro.Domain.Interfaces;
using RevendaPro.Domain.Interfaces.Security;
using RevendaPro.Domain.ValueObjects;
using RevendaPro.Shared.Exceptions;

namespace RevendaPro.Application.Vehicles.Handlers
{
    /// <summary>
    /// Reads the reference table for one vehicle, because somebody asked (RF-14).
    ///
    /// It writes the reference — value, month, model and origin — and <b>no price</b>. What
    /// the dealership wants to take home, the least it accepts and the advertised price stay
    /// untouched: the table suggests by being visible next to them, and the person decides.
    /// See ADR-0005.
    ///
    /// A table out of reach refuses the operation with a reason and leaves the sheet exactly
    /// as it was. Nothing here is allowed to lose the value the vehicle already had.
    /// </summary>
    public class RefreshVehicleFipeHandler(
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IFipeQuoteReader quotes)
        : IRequestHandler<RefreshVehicleFipeCommand, FipeReferenceDto>
    {
        /// <inheritdoc/>
        public async Task<FipeReferenceDto> Handle(
            RefreshVehicleFipeCommand request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var vehicle = await unitOfWork.VehicleRepository
                .GetByCodeAsync(currentUser.IdTenant, request.Code, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new NotFoundException("Veículo inexistente.");

            if (string.IsNullOrWhiteSpace(vehicle.FipeCode))
            {
                // The chooser that finds the code from brand, model and year is the next
                // step of this milestone. Until it exists, the code is typed on the sheet.
                throw new BusinessRuleException(
                    "Informe o código da FIPE deste modelo na ficha para consultar a tabela.");
            }

            var yearFuel = await YearFuelOfAsync(vehicle, cancellationToken).ConfigureAwait(false);

            var quote = await quotes
                .GetCurrentAsync(vehicle.FipeCode, yearFuel, cancellationToken)
                .ConfigureAwait(false);

            if (!quote.Ok)
            {
                throw Refused(quote.Outcome);
            }

            var previous = vehicle.FipeValue;
            var actor = currentUser.Code.ToString();

            vehicle.ApplyFipeReference(
                quote.Value!.Value,
                quote.Value.ReferenceMonth,
                quote.Value.FipeCode,
                quote.Value.YearFuel,
                actor);

            unitOfWork.VehicleRepository.Update(vehicle);

            unitOfWork.AuditLogRepository.Add(AuditLog.Create(
                currentUser.IdTenant, currentUser.Id, nameof(Vehicle), vehicle.Code,
                AuditAction.Update, oldValues: null, newValues: null));

            // One commit for the vehicle and for the quote the reader enqueued: either the
            // table answered and both land, or neither does.
            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

            return new FipeReferenceDto(
                quote.Value.Value,
                quote.Value.ReferenceMonth,
                quote.Value.FipeCode,
                quote.Value.YearFuel,
                FipeSource.Automatic,
                quote.Value.Brand,
                quote.Value.Model,
                previous);
        }

        /// <summary>
        /// The year-fuel pair of the vehicle, found from the model year when it is missing.
        ///
        /// Every car registered before this milestone has a code and no pair, and asking a
        /// person to type <c>2014-5</c> would be asking them to know the shape of a mirror.
        /// </summary>
        private async Task<string> YearFuelOfAsync(Vehicle vehicle, CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(vehicle.FipeYearFuel))
            {
                return vehicle.FipeYearFuel;
            }

            var found = await quotes
                .ResolveYearFuelAsync(vehicle.FipeCode!, vehicle.ModelYear, cancellationToken)
                .ConfigureAwait(false);

            if (found.Ok)
            {
                return found.Value!.YearFuel;
            }

            throw found.Outcome == FipeOutcome.Missing
                ? new BusinessRuleException(
                    $"A tabela FIPE segue sem uma linha única para {vehicle.Brand} "
                    + $"{vehicle.Model} {vehicle.ModelYear}. Confira o código do modelo na ficha.")
                : Refused(found.Outcome);
        }

        /// <summary>
        /// Turns a table that stayed quiet into a refusal with a reason, and with the sheet
        /// intact. The technical detail stays in the log, where the adapter already put it.
        /// </summary>
        private static BusinessRuleException Refused(FipeOutcome outcome) =>
            outcome == FipeOutcome.Missing
                ? new BusinessRuleException(
                    "A tabela FIPE segue sem este modelo. O valor atual continua na ficha.")
                : new BusinessRuleException(
                    "A tabela FIPE está fora de alcance agora. Tente de novo em alguns minutos: "
                    + "o valor atual continua na ficha.");
    }
}
