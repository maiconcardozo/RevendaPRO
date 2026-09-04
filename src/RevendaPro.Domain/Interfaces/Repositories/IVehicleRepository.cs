using Foundation.Domain.Interfaces.Repositories;
using RevendaPro.Domain.Entities;
using RevendaPro.Domain.Enums;
using RevendaPro.Domain.ValueObjects;

namespace RevendaPro.Domain.Interfaces.Repositories
{
    /// <summary>
    /// Extends the conventional CRUD with what only this project knows how to ask about a
    /// vehicle. No IQueryable: Entity Framework is used solely for migrations and mappings.
    /// See ADR-0003.
    /// </summary>
    public interface IVehicleRepository : IDapperRepository<Vehicle>
    {
        /// <summary>Finds a vehicle of a tenant by its public code.</summary>
        /// <param name="idTenant">Owning tenant.</param>
        /// <param name="code">Public identifier.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The vehicle, or null.</returns>
        Task<Vehicle?> GetByCodeAsync(
            int idTenant,
            Guid code,
            CancellationToken cancellationToken = default);

        /// <summary>Lists the vehicles of a tenant, filtered (RF-25).</summary>
        /// <param name="idTenant">Owning tenant.</param>
        /// <param name="search">Matches plate, brand, model, version or chassis.</param>
        /// <param name="status">Restricts to one status.</param>
        /// <param name="origin">Restricts to one origin.</param>
        /// <param name="purchasedFrom">First day of the period, by purchase date.</param>
        /// <param name="purchasedTo">Last day of the period, by purchase date.</param>
        /// <param name="idYard">Restricts to one pátio.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The vehicles.</returns>
        /// <remarks>
        /// A vehicle with no purchase date stays out whenever a period is asked for: it
        /// cannot be said to have entered the yard between two days.
        /// </remarks>
        Task<IReadOnlyList<Vehicle>> ListAsync(
            int idTenant,
            string? search,
            VehicleStatus? status,
            VehicleOrigin? origin,
            DateOnly? purchasedFrom,
            DateOnly? purchasedTo,
            int? idYard,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Everything that happened to a vehicle, oldest first (RF-26).
        ///
        /// Reads the tables of the operation — purchase, moves, expenses, attachments,
        /// proposals and sale — and never the audit log, which keeps values as JSON and
        /// exists to answer who touched what.
        /// </summary>
        /// <param name="idVehicle">Internal identifier of the vehicle.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The events, in the order they happened.</returns>
        Task<IReadOnlyList<VehicleTimelineEntry>> ListTimelineAsync(
            int idVehicle,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Whether the plate or the chassis already belongs to another vehicle of the tenant.
        /// </summary>
        /// <param name="idTenant">Owning tenant.</param>
        /// <param name="plate">Plate, already bare.</param>
        /// <param name="chassis">Chassis, already bare.</param>
        /// <param name="ignoreId">Vehicle to leave out, when editing.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>True when either is taken.</returns>
        /// <summary>
        /// Where every vehicle of a dealership stands against the reference table.
        ///
        /// Each amount meets the quote of its own month: the purchase against the table of
        /// the month it was bought, the sale against the month it closed, what is being asked
        /// against the table of now. See ADR-0005.
        /// </summary>
        /// <param name="idTenant">Owning tenant.</param>
        /// <param name="currentMonth">Month of the table published now.</param>
        /// <param name="today">Today, for how long each car has been on the lot.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>One position per vehicle.</returns>
        Task<IReadOnlyList<MarketPosition>> ListMarketPositionsAsync(
            int idTenant,
            DateOnly currentMonth,
            DateOnly today,
            CancellationToken cancellationToken = default);

        /// <summary>The offers still on the table, against the table of this month.</summary>
        /// <param name="idTenant">Owning tenant.</param>
        /// <param name="currentMonth">Month of the table published now.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The open proposals.</returns>
        Task<IReadOnlyList<MarketProposal>> ListMarketProposalsAsync(
            int idTenant,
            DateOnly currentMonth,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// The cars of the yard whose reference is older than the published table, across
        /// every company.
        ///
        /// Only the monthly routine calls it. There is no tenant here because there is no
        /// person: a scheduled job has no session to isolate. See ADR-0005.
        /// </summary>
        /// <param name="publishedMonth">Month of the table published now.</param>
        /// <param name="limit">Most rows to bring back in one run.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The vehicles, grouped by model so the run reuses each quote.</returns>
        Task<IReadOnlyList<Vehicle>> ListBehindFipeAsync(
            DateOnly publishedMonth,
            int limit,
            CancellationToken cancellationToken = default);

        Task<bool> IdentifierExistsAsync(
            int idTenant,
            string plate,
            string chassis,
            int? ignoreId,
            CancellationToken cancellationToken = default);
    }

    /// <summary>Expenses of a vehicle (RF-08).</summary>
    public interface IVehicleExpenseRepository : IDapperRepository<VehicleExpense>
    {
        /// <summary>Expenses of one vehicle, newest first.</summary>
        /// <param name="idVehicle">The vehicle.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The expenses.</returns>
        Task<IReadOnlyList<VehicleExpense>> ListByVehicleAsync(
            int idVehicle,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Expenses of several vehicles at once, so a listing computes the cost of every row
        /// with one query instead of one per vehicle.
        /// </summary>
        /// <param name="idVehicles">The vehicles.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The expenses, to be grouped by vehicle.</returns>
        Task<IReadOnlyList<VehicleExpense>> ListByVehiclesAsync(
            IReadOnlyCollection<int> idVehicles,
            CancellationToken cancellationToken = default);

        /// <summary>Finds an expense by its public code.</summary>
        /// <param name="code">Public identifier.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The expense, or null.</returns>
        /// <remarks>
        /// Declared again, and hiding the one on the base interface on purpose: reading goes
        /// through a query object, which is where <c>SoftDeleteTests</c> checks that every
        /// SELECT carries <c>IsActive = 1</c>. See ADR-0003.
        /// </remarks>
        new Task<VehicleExpense?> GetByCodeAsync(Guid code, CancellationToken cancellationToken = default);

        /// <summary>
        /// Descriptions this dealership already used, for the suggestion that makes the second
        /// entry faster than the first: picking a known description brings its type along.
        /// </summary>
        /// <param name="idTenant">Owning tenant.</param>
        /// <param name="term">What the user has typed so far.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The most used descriptions that match, most used first.</returns>
        Task<IReadOnlyList<UsedExpenseDescription>> SuggestDescriptionsAsync(
            int idTenant,
            string term,
            CancellationToken cancellationToken = default);
    }

    /// <summary>A description already used, with the type it was filed under.</summary>
    /// <param name="Description">What was typed.</param>
    /// <param name="IdExpenseType">Which type it was filed under.</param>
    /// <param name="Uses">How many times, so the most frequent comes first.</param>
    public sealed record UsedExpenseDescription(string Description, int IdExpenseType, int Uses);

    /// <summary>
    /// Kinds of expense, maintained by the dealership (RF-09).
    /// </summary>
    public interface IExpenseTypeRepository : IDapperRepository<ExpenseType>
    {
        /// <summary>The types of a tenant, in the order they are shown.</summary>
        /// <param name="idTenant">Owning tenant.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The types.</returns>
        Task<IReadOnlyList<ExpenseType>> ListByTenantAsync(
            int idTenant,
            CancellationToken cancellationToken = default);

        /// <summary>Finds a type by its public code.</summary>
        /// <param name="idTenant">Owning tenant.</param>
        /// <param name="code">Public identifier.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The type, or null.</returns>
        Task<ExpenseType?> GetByCodeAsync(
            int idTenant,
            Guid code,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// How many expenses point at a type, so deleting one in use is refused with a reason
        /// instead of leaving rows pointing at nothing.
        /// </summary>
        /// <param name="idExpenseType">The type.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>How many expenses use it.</returns>
        Task<int> CountExpensesAsync(int idExpenseType, CancellationToken cancellationToken = default);
    }

    /// <summary>Photos of a vehicle (RF-12).</summary>
    public interface IVehiclePhotoRepository : IDapperRepository<VehiclePhoto>
    {
        /// <summary>Photos of one vehicle, in gallery order.</summary>
        /// <param name="idVehicle">The vehicle.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The photos.</returns>
        Task<IReadOnlyList<VehiclePhoto>> ListByVehicleAsync(
            int idVehicle,
            CancellationToken cancellationToken = default);

        /// <summary>Finds a photo by its public code.</summary>
        /// <param name="code">Public identifier.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The photo, or null.</returns>
        /// <remarks>Hides the base declaration on purpose. See <see cref="IVehicleExpenseRepository"/>.</remarks>
        new Task<VehiclePhoto?> GetByCodeAsync(Guid code, CancellationToken cancellationToken = default);

        /// <summary>
        /// How many photos each vehicle has, and the key of the one that opens it.
        ///
        /// One query for the whole page, and not one per vehicle: a yard with fifty cars
        /// would otherwise cost fifty round trips to draw one listing. Only the cover travels,
        /// because the listing shows one picture per row.
        /// </summary>
        /// <param name="idVehicles">The vehicles.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>One entry per vehicle that has at least one photo.</returns>
        Task<IReadOnlyList<VehicleGallery>> SummarizeAsync(
            IReadOnlyCollection<int> idVehicles,
            CancellationToken cancellationToken = default);
    }

    /// <summary>What a listing needs to know about the gallery of one vehicle.</summary>
    /// <param name="IdVehicle">The vehicle.</param>
    /// <param name="PhotoCount">How many photos it has.</param>
    /// <param name="CoverStorageKey">Key of the cover, or null while none was chosen.</param>
    public sealed record VehicleGallery(int IdVehicle, int PhotoCount, string? CoverStorageKey);

    /// <summary>Documents of a vehicle (RF-13).</summary>
    public interface IVehicleDocumentRepository : IDapperRepository<VehicleDocument>
    {
        /// <summary>Documents of one vehicle, newest first.</summary>
        /// <param name="idVehicle">The vehicle.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The documents.</returns>
        Task<IReadOnlyList<VehicleDocument>> ListByVehicleAsync(
            int idVehicle,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Documents taken out of the file of a vehicle, newest deletion first, with the
        /// vehicle each one belonged to.
        /// </summary>
        /// <param name="idTenant">Owning tenant.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The deleted documents.</returns>
        Task<IReadOnlyList<DeletedVehicleDocument>> ListDeletedByTenantAsync(
            int idTenant,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Finds a document by code even when deleted. Only the restore path uses it.
        /// </summary>
        /// <param name="code">Public identifier.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The document, or null.</returns>
        Task<VehicleDocument?> GetByCodeIncludingDeletedAsync(
            Guid code,
            CancellationToken cancellationToken = default);

        /// <summary>Finds a document by its public code.</summary>
        /// <param name="code">Public identifier.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The document, or null.</returns>
        /// <remarks>Hides the base declaration on purpose. See <see cref="IVehicleExpenseRepository"/>.</remarks>
        new Task<VehicleDocument?> GetByCodeAsync(Guid code, CancellationToken cancellationToken = default);
    }

    /// <summary>As passagens de um veículo pelos pátios.</summary>
    public interface IVehicleYardHistoryRepository : IDapperRepository<VehicleYardHistory>
    {
        /// <summary>As passagens de um veículo, da mais antiga para a mais nova.</summary>
        /// <param name="idVehicle">O veículo.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>As passagens.</returns>
        Task<IReadOnlyList<VehicleYardHistory>> ListByVehicleAsync(
            int idVehicle,
            CancellationToken cancellationToken = default);
    }

    /// <summary>Status history of a vehicle (RF-26).</summary>
    public interface IVehicleStatusHistoryRepository : IDapperRepository<VehicleStatusHistory>
    {
        /// <summary>The moves of one vehicle, oldest first.</summary>
        /// <param name="idVehicle">The vehicle.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The history.</returns>
        Task<IReadOnlyList<VehicleStatusHistory>> ListByVehicleAsync(
            int idVehicle,
            CancellationToken cancellationToken = default);
    }
}
