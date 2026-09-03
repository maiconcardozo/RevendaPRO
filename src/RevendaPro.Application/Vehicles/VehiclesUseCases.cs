using RevendaPro.Domain.Enums;

namespace RevendaPro.Application.Vehicles.DTOs
{
    /// <summary>
    /// A vehicle as the screen reads it, cost included.
    ///
    /// The cost fields are computed on every read, never stored. See <c>VehicleCost</c>.
    /// </summary>
    /// <param name="Code">Public identifier. The internal Id is never exposed.</param>
    /// <param name="Plate">Plate, bare. The mask lives in the UI.</param>
    /// <param name="Chassis">Chassis.</param>
    /// <param name="Brand">Brand.</param>
    /// <param name="Model">Model.</param>
    /// <param name="Version">Version or trim.</param>
    /// <param name="ModelYear">Model year.</param>
    /// <param name="ManufactureYear">Manufacture year.</param>
    /// <param name="Color">Colour.</param>
    /// <param name="Mileage">Kilometres.</param>
    /// <param name="FuelType">Fuel.</param>
    /// <param name="Transmission">Transmission.</param>
    /// <param name="Renavam">Renavam.</param>
    /// <param name="Origin">Where it came from.</param>
    /// <param name="HasDamage">Whether it was in a crash.</param>
    /// <param name="DamageDescription">What happened.</param>
    /// <param name="Status">Where it is in the pipeline.</param>
    /// <param name="AllowedStatuses">Where it can go from here, so the screen offers only those.</param>
    /// <param name="PurchasePrice">What was paid.</param>
    /// <param name="PurchaseDate">When.</param>
    /// <param name="SupplierName">Supplier or auction house.</param>
    /// <param name="PurchasePaymentMethod">How it was paid.</param>
    /// <param name="BudgetCeiling">The most it is meant to cost in total.</param>
    /// <param name="FipeValue">Reference value.</param>
    /// <param name="FipeReferenceDate">Which month it came from.</param>
    /// <param name="FipeCode">Code of the exact model, when known.</param>
    /// <param name="DesiredNetPrice">What the dealership wants to take home.</param>
    /// <param name="MinimumNetPrice">The least it accepts.</param>
    /// <param name="AdvertisedPrice">Advertised price.</param>
    /// <param name="MarketNotes">What comparable cars are asking.</param>
    /// <param name="Notes">Free notes.</param>
    /// <param name="Cost">Everything the cost says about this vehicle.</param>
    /// <param name="DaysInStock">How long it has been in stock.</param>
    /// <param name="PhotoCount">How many photos it has.</param>
    /// <param name="CoverThumbnailUrl">
    /// Signed address of the smallest rendition of the cover, or null while the vehicle has no
    /// photo. The listing shows this one, and never the full size: a yard screen with fifty
    /// cars would otherwise pull tens of megabytes to fill small squares.
    /// </param>
    public sealed record VehicleDto(
        Guid Code,
        string Plate,
        string Chassis,
        string Brand,
        string Model,
        string? Version,
        short ModelYear,
        short ManufactureYear,
        string? Color,
        int Mileage,
        FuelType FuelType,
        TransmissionType Transmission,
        string? Renavam,
        VehicleOrigin Origin,
        bool HasDamage,
        string? DamageDescription,
        VehicleStatus Status,
        IReadOnlyList<VehicleStatus> AllowedStatuses,
        decimal PurchasePrice,
        DateOnly? PurchaseDate,
        string? SupplierName,
        PaymentMethod? PurchasePaymentMethod,
        decimal? BudgetCeiling,
        decimal? FipeValue,
        DateOnly? FipeReferenceDate,
        string? FipeCode,
        decimal? DesiredNetPrice,
        decimal? MinimumNetPrice,
        decimal? AdvertisedPrice,
        string? MarketNotes,
        string? Notes,
        VehicleCostDto Cost,
        int? DaysInStock,
        int PhotoCount,
        string? CoverThumbnailUrl);

    /// <summary>
    /// What the vehicle cost, and what that means for a price.
    ///
    /// Every number here is calculated. None of them exists as a column, which is the whole
    /// point: a stored total is right until the next expense and wrong from then on.
    /// </summary>
    /// <param name="Purchase">What was paid for the vehicle.</param>
    /// <param name="PaidExpenses">Everything already spent.</param>
    /// <param name="PlannedExpenses">What is expected and still unpaid.</param>
    /// <param name="Total">Purchase plus what was paid.</param>
    /// <param name="Projected">Where the cost lands if everything planned is spent.</param>
    /// <param name="BudgetUsedPercent">How much of the ceiling is gone.</param>
    /// <param name="BudgetRemaining">How much room is left. Negative once past the ceiling.</param>
    /// <param name="IsOverBudget">Whether it already costs more than intended.</param>
    /// <param name="WillExceedBudget">Whether the planned expenses take it past the ceiling.</param>
    /// <param name="PercentOfFipe">What the cost represents against the reference table.</param>
    /// <param name="ProfitAtDesired">Profit if it sells for the desired price.</param>
    /// <param name="MarginAtDesired">Margin at the desired price.</param>
    public sealed record VehicleCostDto(
        decimal Purchase,
        decimal PaidExpenses,
        decimal PlannedExpenses,
        decimal Total,
        decimal Projected,
        decimal? BudgetUsedPercent,
        decimal? BudgetRemaining,
        bool IsOverBudget,
        bool WillExceedBudget,
        decimal? PercentOfFipe,
        decimal? ProfitAtDesired,
        decimal? MarginAtDesired);

    /// <summary>
    /// One thing that happened to the vehicle, in the single history the file shows (RF-26).
    ///
    /// The screen decides how to word each kind, which is why almost everything here is
    /// data and not a sentence: the label of a status, the wording of an expense and the
    /// plural of "photos" are text the user reads, and text the user reads lives in the
    /// frontend. See ADR-0003.
    /// </summary>
    /// <param name="Moment">When it happened.</param>
    /// <param name="Kind">What kind of thing happened.</param>
    /// <param name="Code">Public identifier, so the screen can link to it. Null when several were counted.</param>
    /// <param name="Title">What the data itself says. Null when several were counted.</param>
    /// <param name="Detail">The note somebody wrote, or the reason for a move.</param>
    /// <param name="Amount">Money, when the event has money.</param>
    /// <param name="Quantity">How many records this entry stands for.</param>
    /// <param name="FromStatus">Where the vehicle came from. Only on a move.</param>
    /// <param name="ToStatus">Where the vehicle went. Only on a move.</param>
    /// <param name="ProposalStatus">Whether the offer was accepted, refused or is still open.</param>
    /// <param name="IsPaid">Whether the expense was paid, or is still planned.</param>
    /// <param name="ActorName">
    /// Who did it, by name. Null when the system did it, or when the user no longer exists:
    /// an event is never hidden for missing an author.
    /// </param>
    public sealed record VehicleTimelineEntryDto(
        DateTime Moment,
        TimelineEventKind Kind,
        Guid? Code,
        string? Title,
        string? Detail,
        decimal? Amount,
        int Quantity,
        VehicleStatus? FromStatus,
        VehicleStatus? ToStatus,
        ProposalStatus? ProposalStatus,
        bool? IsPaid,
        string? ActorName);
}

namespace RevendaPro.Application.Vehicles.Queries
{
    using MediatR;
    using RevendaPro.Application.Vehicles.DTOs;

    /// <summary>Lists the vehicles of the tenant, filtered (RF-25).</summary>
    /// <param name="Search">Matches plate, brand, model, version or chassis.</param>
    /// <param name="Status">Restricts to one status.</param>
    /// <param name="Origin">Restricts to one origin.</param>
    /// <param name="PurchasedFrom">First day of the period, by purchase date.</param>
    /// <param name="PurchasedTo">Last day of the period, by purchase date.</param>
    public sealed record ListVehiclesQuery(
        string? Search = null,
        VehicleStatus? Status = null,
        VehicleOrigin? Origin = null,
        DateOnly? PurchasedFrom = null,
        DateOnly? PurchasedTo = null) : IRequest<IReadOnlyList<VehicleDto>>;

    /// <summary>Reads one vehicle.</summary>
    /// <param name="Code">Public identifier.</param>
    public sealed record GetVehicleQuery(Guid Code) : IRequest<VehicleDto>;

    /// <summary>Reads everything that happened to one vehicle, oldest first (RF-26).</summary>
    /// <param name="Code">Public identifier of the vehicle.</param>
    public sealed record GetVehicleTimelineQuery(Guid Code)
        : IRequest<IReadOnlyList<VehicleTimelineEntryDto>>;
}

namespace RevendaPro.Application.Vehicles.Commands
{
    using MediatR;
    using RevendaPro.Application.Vehicles.DTOs;

    /// <summary>Creates or updates a vehicle.</summary>
    /// <param name="Code">Null creates; filled updates.</param>
    /// <param name="Plate">Plate, in either Brazilian format.</param>
    /// <param name="Chassis">Chassis.</param>
    /// <param name="Brand">Brand.</param>
    /// <param name="Model">Model.</param>
    /// <param name="Version">Version or trim.</param>
    /// <param name="ModelYear">Model year.</param>
    /// <param name="ManufactureYear">Manufacture year.</param>
    /// <param name="Color">Colour.</param>
    /// <param name="Mileage">Kilometres.</param>
    /// <param name="MileageCorrection">True to accept a reading lower than the current one.</param>
    /// <param name="FuelType">Fuel.</param>
    /// <param name="Transmission">Transmission.</param>
    /// <param name="Renavam">Renavam.</param>
    /// <param name="Origin">Where it came from.</param>
    /// <param name="HasDamage">Whether it was in a crash.</param>
    /// <param name="DamageDescription">What happened.</param>
    /// <param name="PurchasePrice">What was paid.</param>
    /// <param name="PurchaseDate">When.</param>
    /// <param name="SupplierName">Supplier or auction house.</param>
    /// <param name="PurchasePaymentMethod">How it was paid.</param>
    /// <param name="BudgetCeiling">The most it is meant to cost in total.</param>
    /// <param name="FipeValue">Reference value.</param>
    /// <param name="FipeReferenceDate">Which month it came from.</param>
    /// <param name="FipeCode">Code of the exact model, when known.</param>
    /// <param name="DesiredNetPrice">What the dealership wants to take home.</param>
    /// <param name="MinimumNetPrice">The least it accepts.</param>
    /// <param name="AdvertisedPrice">Advertised price.</param>
    /// <param name="MarketNotes">What comparable cars are asking.</param>
    /// <param name="Notes">Free notes.</param>
    public sealed record SaveVehicleCommand(
        Guid? Code,
        string Plate,
        string Chassis,
        string Brand,
        string Model,
        string? Version,
        short ModelYear,
        short ManufactureYear,
        string? Color,
        int Mileage,
        bool MileageCorrection,
        FuelType FuelType,
        TransmissionType Transmission,
        string? Renavam,
        VehicleOrigin Origin,
        bool HasDamage,
        string? DamageDescription,
        decimal PurchasePrice,
        DateOnly? PurchaseDate,
        string? SupplierName,
        PaymentMethod? PurchasePaymentMethod,
        decimal? BudgetCeiling,
        decimal? FipeValue,
        DateOnly? FipeReferenceDate,
        string? FipeCode,
        decimal? DesiredNetPrice,
        decimal? MinimumNetPrice,
        decimal? AdvertisedPrice,
        string? MarketNotes,
        string? Notes) : IRequest<VehicleDto>;

    /// <summary>Moves the vehicle along the pipeline (RF-06).</summary>
    /// <param name="Code">Public identifier.</param>
    /// <param name="Status">Where it goes.</param>
    /// <param name="Reason">Why, when there is a reason.</param>
    public sealed record ChangeVehicleStatusCommand(Guid Code, VehicleStatus Status, string? Reason)
        : IRequest;

    /// <summary>Soft deletes a vehicle.</summary>
    /// <param name="Code">Public identifier.</param>
    public sealed record DeleteVehicleCommand(Guid Code) : IRequest;
}
