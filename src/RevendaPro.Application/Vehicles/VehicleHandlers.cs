using FluentValidation;
using MediatR;
using RevendaPro.Application.Vehicles.Commands;
using RevendaPro.Application.Vehicles.DTOs;
using RevendaPro.Application.Vehicles.Queries;
using RevendaPro.Domain.Entities;
using RevendaPro.Domain.Enums;
using RevendaPro.Domain.Interfaces;
using RevendaPro.Domain.Interfaces.Security;
using RevendaPro.Domain.Interfaces.Storage;
using RevendaPro.Domain.ValueObjects;
using RevendaPro.Shared.Exceptions;
using RevendaPro.Shared.Helpers;

namespace RevendaPro.Application.Vehicles.Validators
{
    /// <summary>
    /// What the screen has to answer before the domain is even asked.
    ///
    /// The domain repeats the identifier rules on purpose: a command is one way in, and the
    /// entity has to hold regardless of who calls it.
    /// </summary>
    public class SaveVehicleValidator : AbstractValidator<SaveVehicleCommand>
    {
        /// <summary>Builds the rules.</summary>
        public SaveVehicleValidator()
        {
            RuleFor(c => c.Plate)
                .NotEmpty().WithMessage("Informe a placa.")
                .Must(VehicleIdentifiers.IsValidPlate)
                .WithMessage("Placa inválida. Use ABC1234 ou ABC1D23.");

            RuleFor(c => c.Chassis)
                .NotEmpty().WithMessage("Informe o chassi.")
                .Must(VehicleIdentifiers.IsValidChassis)
                .WithMessage("Chassi inválido. São 17 caracteres, sem as letras I, O e Q.");

            RuleFor(c => c.Brand)
                .NotEmpty().WithMessage("Informe a marca.")
                .MaximumLength(60).WithMessage("A marca pode ter no máximo 60 caracteres.");

            RuleFor(c => c.Model)
                .NotEmpty().WithMessage("Informe o modelo.")
                .MaximumLength(80).WithMessage("O modelo pode ter no máximo 80 caracteres.");

            RuleFor(c => c.ModelYear)
                .InclusiveBetween((short)1900, (short)2100)
                .WithMessage("Informe um ano de modelo válido.");

            RuleFor(c => c.ManufactureYear)
                .InclusiveBetween((short)1900, (short)2100)
                .WithMessage("Informe um ano de fabricação válido.");

            RuleFor(c => c.ModelYear)
                .GreaterThanOrEqualTo(c => c.ManufactureYear)
                .WithMessage("O ano do modelo é igual ou posterior ao ano de fabricação.");

            RuleFor(c => c.Mileage)
                .GreaterThanOrEqualTo(0).WithMessage("Informe uma quilometragem válida.");

            RuleFor(c => c.PurchasePrice)
                .GreaterThanOrEqualTo(0).WithMessage("Informe um valor de compra válido.");

            RuleFor(c => c.DamageDescription)
                .NotEmpty().WithMessage("Descreva o sinistro do veículo.")
                .When(c => c.HasDamage);

            RuleFor(c => c.BudgetCeiling)
                .GreaterThan(0).WithMessage("Informe um teto de orçamento maior que zero.")
                .When(c => c.BudgetCeiling is not null);

            RuleFor(c => c.FipeReferenceDate)
                .NotNull().WithMessage("Informe o mês de referência da FIPE.")
                .When(c => c.FipeValue is not null);

            RuleFor(c => c.MinimumNetPrice)
                .LessThanOrEqualTo(c => c.DesiredNetPrice)
                .WithMessage("O preço mínimo aceito é igual ou menor que o preço desejado.")
                .When(c => c.MinimumNetPrice is not null && c.DesiredNetPrice is not null);
        }
    }
}

namespace RevendaPro.Application.Vehicles.Handlers
{
    /// <summary>Turns a vehicle and its expenses into what the screen reads.</summary>
    internal static class VehicleMapper
    {
        /// <summary>Builds the DTO of one vehicle.</summary>
        /// <param name="vehicle">The vehicle.</param>
        /// <param name="expenses">Its expenses, paid and planned.</param>
        /// <param name="photoCount">How many photos it has.</param>
        /// <param name="coverThumbnailUrl">Signed address of the cover, smallest rendition.</param>
        /// <param name="today">Today, passed in so the calculation stays testable.</param>
        /// <returns>The vehicle as the screen reads it.</returns>
        public static VehicleDto ToDto(
            Vehicle vehicle,
            IReadOnlyCollection<VehicleExpense> expenses,
            int photoCount,
            string? coverThumbnailUrl,
            DateOnly today)
        {
            var cost = VehicleCost.Of(vehicle, expenses);

            return new VehicleDto(
                vehicle.Code,
                vehicle.Plate,
                vehicle.Chassis,
                vehicle.Brand,
                vehicle.Model,
                vehicle.Version,
                vehicle.ModelYear,
                vehicle.ManufactureYear,
                vehicle.Color,
                vehicle.Mileage,
                vehicle.FuelType,
                vehicle.Transmission,
                vehicle.Renavam,
                vehicle.Origin,
                vehicle.HasDamage,
                vehicle.DamageDescription,
                vehicle.Status,

                // The screen offers only what the pipeline allows, so a move is refused before
                // it is attempted rather than after.
                [.. Enum.GetValues<VehicleStatus>().Where(vehicle.CanChangeTo)],

                vehicle.PurchasePrice,
                vehicle.PurchaseDate,
                vehicle.SupplierName,
                vehicle.PurchasePaymentMethod,
                vehicle.BudgetCeiling,
                vehicle.FipeValue,
                vehicle.FipeReferenceDate,
                vehicle.FipeCode,
                vehicle.DesiredNetPrice,
                vehicle.MinimumNetPrice,
                vehicle.AdvertisedPrice,
                vehicle.MarketNotes,
                vehicle.Notes,
                ToDto(cost, vehicle.DesiredNetPrice),
                vehicle.DaysInStock(today),
                photoCount,
                coverThumbnailUrl);
        }

        private static VehicleCostDto ToDto(VehicleCost cost, decimal? desiredPrice) =>
            new(cost.Purchase,
                cost.PaidExpenses,
                cost.PlannedExpenses,
                cost.Total,
                cost.Projected,
                cost.BudgetUsedPercent,
                cost.BudgetRemaining,
                cost.IsOverBudget,
                cost.WillExceedBudget,
                cost.PercentOfFipe,
                desiredPrice is > 0 ? cost.ProfitAt(desiredPrice.Value) : null,
                desiredPrice is > 0 ? cost.MarginAt(desiredPrice.Value) : null);
    }

    /// <summary>Lists the vehicles of the tenant with their cost (RF-25).</summary>
    public class ListVehiclesHandler(
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IFileStorage storage)
        : IRequestHandler<ListVehiclesQuery, IReadOnlyList<VehicleDto>>
    {
        /// <inheritdoc/>
        public async Task<IReadOnlyList<VehicleDto>> Handle(
            ListVehiclesQuery request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var vehicles = await unitOfWork.VehicleRepository
                .ListAsync(currentUser.IdTenant, request.Search, request.Status, request.Origin,
                    request.PurchasedFrom, request.PurchasedTo, cancellationToken)
                .ConfigureAwait(false);

            if (vehicles.Count == 0)
            {
                return [];
            }

            // One query for every expense of every listed vehicle, and not one query per
            // vehicle: a yard with fifty cars would otherwise cost fifty round trips to show
            // one page.
            var expenses = await unitOfWork.VehicleExpenseRepository
                .ListByVehiclesAsync([.. vehicles.Select(v => v.Id)], cancellationToken)
                .ConfigureAwait(false);

            var byVehicle = expenses
                .GroupBy(expense => expense.IdVehicle)
                .ToDictionary(group => group.Key, group => (IReadOnlyCollection<VehicleExpense>)[.. group]);

            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            var galleries = await VehicleGalleries
                .ForAsync(unitOfWork, storage, [.. vehicles.Select(v => v.Id)], cancellationToken)
                .ConfigureAwait(false);

            return [.. vehicles.Select(vehicle =>
            {
                var cover = galleries.GetValueOrDefault(vehicle.Id);

                return VehicleMapper.ToDto(
                    vehicle,
                    byVehicle.TryGetValue(vehicle.Id, out var found) ? found : [],
                    cover?.PhotoCount ?? 0,
                    cover?.ThumbnailUrl,
                    today);
            })];
        }
    }

    /// <summary>Reads one vehicle.</summary>
    public class GetVehicleHandler(
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IFileStorage storage)
        : IRequestHandler<GetVehicleQuery, VehicleDto>
    {
        /// <inheritdoc/>
        public async Task<VehicleDto> Handle(GetVehicleQuery request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var vehicle = await unitOfWork.VehicleRepository
                .GetByCodeAsync(currentUser.IdTenant, request.Code, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new NotFoundException("Veículo inexistente.");

            var expenses = await unitOfWork.VehicleExpenseRepository
                .ListByVehicleAsync(vehicle.Id, cancellationToken)
                .ConfigureAwait(false);

            var cover = (await VehicleGalleries
                .ForAsync(unitOfWork, storage, [vehicle.Id], cancellationToken)
                .ConfigureAwait(false))
                .GetValueOrDefault(vehicle.Id);

            return VehicleMapper.ToDto(
                vehicle, expenses, cover?.PhotoCount ?? 0, cover?.ThumbnailUrl,
                DateOnly.FromDateTime(DateTime.UtcNow));
        }
    }

    /// <summary>
    /// Reads the whole story of one vehicle, in order (RF-26).
    ///
    /// Two readings, and no more: the events, which the database sorts, and the users of the
    /// tenant, which turn the code every table stores into the name a person recognizes.
    ///
    /// The second reading asks for deleted users as well. Somebody who left the dealership
    /// still did what they did, and a history that forgets the author on the day the account
    /// is closed is a history that rewrites itself. Only the name is taken from them.
    /// </summary>
    public class GetVehicleTimelineHandler(IUnitOfWork unitOfWork, ICurrentUser currentUser)
        : IRequestHandler<GetVehicleTimelineQuery, IReadOnlyList<VehicleTimelineEntryDto>>
    {
        /// <inheritdoc/>
        public async Task<IReadOnlyList<VehicleTimelineEntryDto>> Handle(
            GetVehicleTimelineQuery request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var vehicle = await unitOfWork.VehicleRepository
                .GetByCodeAsync(currentUser.IdTenant, request.Code, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new NotFoundException("Veículo inexistente.");

            var entries = await unitOfWork.VehicleRepository
                .ListTimelineAsync(vehicle.Id, cancellationToken)
                .ConfigureAwait(false);

            var names = await ReadActorNamesAsync(cancellationToken).ConfigureAwait(false);

            return [.. entries.Select(entry => new VehicleTimelineEntryDto(
                entry.Moment,
                entry.Kind,
                entry.Code,
                entry.Title,
                entry.Detail,
                entry.Amount,
                entry.Quantity,
                entry.FromStatus,
                entry.ToStatus,
                entry.ProposalStatus,
                entry.IsPaid,
                NameOf(entry.ActorCode, names)))];
        }

        /// <summary>The users of the tenant by code, deleted ones included.</summary>
        private async Task<Dictionary<string, string>> ReadActorNamesAsync(
            CancellationToken cancellationToken)
        {
            var users = await unitOfWork.UserRepository
                .ListByTenantAsync(currentUser.IdTenant, null, includeDeleted: true, cancellationToken)
                .ConfigureAwait(false);

            return users
                .GroupBy(user => user.Code.ToString(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.First().Name,
                    StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// The name behind a code, when there is one. An unknown author leaves the event in
        /// place without a name: what happened matters more than who typed it.
        /// </summary>
        private static string? NameOf(string? actorCode, Dictionary<string, string> names) =>
            actorCode is not null && names.TryGetValue(actorCode, out var name) ? name : null;
    }

    /// <summary>Creates or updates a vehicle.</summary>
    public class SaveVehicleHandler(
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IFileStorage storage)
        : IRequestHandler<SaveVehicleCommand, VehicleDto>
    {
        /// <inheritdoc/>
        public async Task<VehicleDto> Handle(SaveVehicleCommand request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var idTenant = currentUser.IdTenant;
            var actor = currentUser.Code.ToString();
            var isNew = request.Code is null;

            var plate = VehicleIdentifiers.Normalize(request.Plate);
            var chassis = VehicleIdentifiers.Normalize(request.Chassis);

            Vehicle vehicle;

            if (isNew)
            {
                await EnsureIdentifierIsFreeAsync(idTenant, plate, chassis, null, cancellationToken)
                    .ConfigureAwait(false);

                vehicle = Vehicle.Create(
                    idTenant, plate, chassis, request.Brand, request.Model,
                    request.ModelYear, request.ManufactureYear, actor);

                Apply(vehicle, request);

                unitOfWork.VehicleRepository.Add(vehicle);
                await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

                // Read back so the Id assigned by the database is known before the history.
                vehicle = await unitOfWork.VehicleRepository
                    .GetByCodeAsync(idTenant, vehicle.Code, cancellationToken)
                    .ConfigureAwait(false)
                    ?? throw new BusinessRuleException("Falha ao cadastrar o veículo.");

                unitOfWork.VehicleStatusHistoryRepository.Add(VehicleStatusHistory.Create(
                    vehicle.Id, null, vehicle.Status, "Cadastro", actor));
            }
            else
            {
                vehicle = await unitOfWork.VehicleRepository
                    .GetByCodeAsync(idTenant, request.Code!.Value, cancellationToken)
                    .ConfigureAwait(false)
                    ?? throw new NotFoundException("Veículo inexistente.");

                await EnsureIdentifierIsFreeAsync(
                    idTenant, plate, chassis, vehicle.Id, cancellationToken).ConfigureAwait(false);

                vehicle.SetIdentification(
                    plate, chassis, request.Brand, request.Model,
                    request.ModelYear, request.ManufactureYear);

                Apply(vehicle, request);
                vehicle.UpdateAuditInfo(actor);

                unitOfWork.VehicleRepository.Update(vehicle);
            }

            unitOfWork.AuditLogRepository.Add(AuditLog.Create(
                idTenant, currentUser.Id, nameof(Vehicle), vehicle.Code,
                isNew ? AuditAction.Create : AuditAction.Update, oldValues: null, newValues: null));

            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

            var expenses = await unitOfWork.VehicleExpenseRepository
                .ListByVehicleAsync(vehicle.Id, cancellationToken)
                .ConfigureAwait(false);

            var cover = (await VehicleGalleries
                .ForAsync(unitOfWork, storage, [vehicle.Id], cancellationToken)
                .ConfigureAwait(false))
                .GetValueOrDefault(vehicle.Id);

            return VehicleMapper.ToDto(
                vehicle, expenses, cover?.PhotoCount ?? 0, cover?.ThumbnailUrl,
                DateOnly.FromDateTime(DateTime.UtcNow));
        }

        private static void Apply(Vehicle vehicle, SaveVehicleCommand request)
        {
            vehicle.SetDetails(
                request.Version, request.Color, request.FuelType, request.Transmission,
                request.Renavam, request.Notes);

            vehicle.SetOrigin(request.Origin, request.HasDamage, request.DamageDescription);

            vehicle.SetPurchase(
                request.PurchasePrice, request.PurchaseDate, request.SupplierName,
                request.PurchasePaymentMethod);

            vehicle.SetBudgetCeiling(request.BudgetCeiling);
            vehicle.SetFipe(request.FipeValue, request.FipeReferenceDate, request.FipeCode);

            vehicle.SetPricing(
                request.DesiredNetPrice, request.MinimumNetPrice, request.AdvertisedPrice,
                request.MarketNotes);

            vehicle.UpdateMileage(request.Mileage, request.MileageCorrection);
        }

        /// <summary>
        /// Refuses a plate or a chassis that already belongs to another vehicle of the same
        /// dealership. The message names which of the two, because "already registered" leaves
        /// somebody hunting through a form.
        /// </summary>
        private async Task EnsureIdentifierIsFreeAsync(
            int idTenant,
            string plate,
            string chassis,
            int? ignoreId,
            CancellationToken cancellationToken)
        {
            var taken = await unitOfWork.VehicleRepository
                .IdentifierExistsAsync(idTenant, plate, chassis, ignoreId, cancellationToken)
                .ConfigureAwait(false);

            if (taken)
            {
                throw new BusinessRuleException(
                    $"A placa {plate} ou o chassi {chassis} já pertencem a outro veículo.");
            }
        }
    }

    /// <summary>Moves the vehicle along the pipeline (RF-06).</summary>
    public class ChangeVehicleStatusHandler(IUnitOfWork unitOfWork, ICurrentUser currentUser)
        : IRequestHandler<ChangeVehicleStatusCommand>
    {
        /// <inheritdoc/>
        public async Task Handle(ChangeVehicleStatusCommand request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var vehicle = await unitOfWork.VehicleRepository
                .GetByCodeAsync(currentUser.IdTenant, request.Code, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new NotFoundException("Veículo inexistente.");

            var actor = currentUser.Code.ToString();

            // Sold has one door, and it is the sale: it carries the buyer, the price and the
            // profit that a bare status change would leave blank.
            if (request.Status == VehicleStatus.Sold)
            {
                throw new BusinessRuleException(
                    "Para marcar como vendido, registre a venda na aba Vendas.");
            }

            // The entity refuses an impossible move and answers where it came from, which is
            // what the history needs.
            var previous = vehicle.ChangeStatus(request.Status, actor);

            unitOfWork.VehicleRepository.Update(vehicle);

            unitOfWork.VehicleStatusHistoryRepository.Add(VehicleStatusHistory.Create(
                vehicle.Id, previous, request.Status, request.Reason, actor));

            unitOfWork.AuditLogRepository.Add(AuditLog.Create(
                currentUser.IdTenant, currentUser.Id, nameof(Vehicle), vehicle.Code,
                AuditAction.Update, oldValues: null, newValues: null));

            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Soft deletes a vehicle.</summary>
    public class DeleteVehicleHandler(IUnitOfWork unitOfWork, ICurrentUser currentUser)
        : IRequestHandler<DeleteVehicleCommand>
    {
        /// <inheritdoc/>
        public async Task Handle(DeleteVehicleCommand request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var vehicle = await unitOfWork.VehicleRepository
                .GetByCodeAsync(currentUser.IdTenant, request.Code, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new NotFoundException("Veículo inexistente.");

            var actor = currentUser.Code.ToString();

            // Logical, as everything here is: the photos, the documents and the spending stay
            // where they are, and an administrator can bring the whole thing back (RNF-08).
            unitOfWork.VehicleRepository.Remove(vehicle, actor);

            unitOfWork.AuditLogRepository.Add(AuditLog.Create(
                currentUser.IdTenant, currentUser.Id, nameof(Vehicle), vehicle.Code,
                AuditAction.Delete, oldValues: null, newValues: null));

            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
