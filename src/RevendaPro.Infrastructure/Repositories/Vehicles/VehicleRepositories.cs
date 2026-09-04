using Foundation.Dapper.Repositories;
using Foundation.Domain.Interfaces.UnitOfWork;
using RevendaPro.Domain.Entities;
using RevendaPro.Domain.Enums;
using RevendaPro.Domain.Interfaces.Repositories;
using RevendaPro.Domain.ValueObjects;
using RevendaPro.Infrastructure.Queries.Reference;
using RevendaPro.Infrastructure.Queries.Vehicles;

namespace RevendaPro.Infrastructure.Repositories.Vehicles
{
    /// <summary>Dapper repository for <see cref="Vehicle"/>.</summary>
    public class VehicleRepository(IDapperUnitOfWork unitOfWork)
        : DapperRepository<Vehicle>(unitOfWork), IVehicleRepository
    {
        /// <inheritdoc/>
        public Task<Vehicle?> GetByCodeAsync(
            int idTenant,
            Guid code,
            CancellationToken cancellationToken = default) =>
            QuerySingleAsync(new FindVehicleByCodeQuery(idTenant, code), cancellationToken);

        /// <inheritdoc/>
        public Task<IReadOnlyList<Vehicle>> ListAsync(
            int idTenant,
            string? search,
            VehicleStatus? status,
            VehicleOrigin? origin,
            DateOnly? purchasedFrom,
            DateOnly? purchasedTo,
            CancellationToken cancellationToken = default) =>
            QueryAsync(
                new ListVehiclesQuery(idTenant, search, status, origin, purchasedFrom, purchasedTo),
                cancellationToken);

        /// <inheritdoc/>
        public async Task<IReadOnlyList<VehicleTimelineEntry>> ListTimelineAsync(
            int idVehicle,
            CancellationToken cancellationToken = default)
        {
            var rows = await QueryColumnAsync<TimelineRow>(
                new ListVehicleTimelineQuery(idVehicle), cancellationToken)
                .ConfigureAwait(false);

            return [.. rows.Select(row => new VehicleTimelineEntry(
                row.Moment,
                (TimelineEventKind)row.Kind,
                row.Code,
                row.Title,
                row.Detail,
                row.Amount,
                (int)row.Quantity,
                (VehicleStatus?)row.FromStatus,
                (VehicleStatus?)row.ToStatus,
                (ProposalStatus?)row.ProposalStatus,
                row.IsPaid is null ? null : row.IsPaid.Value != 0,
                row.ActorCode))];
        }

        /// <summary>
        /// The row as the driver hands it over, and never as the domain wants it.
        ///
        /// Dapper matches a constructor by exact type, so this shape is dictated by the
        /// statement: the cast integers arrive as <c>Int32</c>, <c>COUNT(*)</c> as
        /// <c>Int64</c>, and the flag of an expense as a number, because a UNION resolves the
        /// type of a column across every branch and the other branches hold NULL. Turning
        /// those into an enum, an int and a bool is the job of this file, next to the driver,
        /// and never of the contract the domain reads.
        /// </summary>
        private sealed record TimelineRow(
            DateTime Moment,
            int Kind,
            Guid? Code,
            string? Title,
            string? Detail,
            decimal? Amount,
            long Quantity,
            int? FromStatus,
            int? ToStatus,
            int? ProposalStatus,
            int? IsPaid,
            string? ActorCode);

        /// <inheritdoc/>
        public async Task<IReadOnlyList<MarketPosition>> ListMarketPositionsAsync(
            int idTenant,
            DateOnly currentMonth,
            DateOnly today,
            CancellationToken cancellationToken = default)
        {
            var rows = await QueryColumnAsync<MarketRow>(
                new ListMarketPositionsQuery(idTenant, currentMonth, today), cancellationToken)
                .ConfigureAwait(false);

            return [.. rows.Select(row => new MarketPosition(
                row.Code,
                row.Plate,
                row.Brand,
                row.Model,
                row.Version,
                row.ModelYear,
                (VehicleStatus)row.Status,
                row.DaysInStock,
                row.PurchasePrice,
                Day(row.PurchaseDate),
                row.PurchaseReference,
                row.DesiredNetPrice,
                row.CurrentReference,
                row.PreviousReference,
                row.SaleAmount,
                Day(row.SaleDate),
                row.SaleReference))];
        }

        /// <summary>
        /// The row as the driver hands it over, and never as the domain wants it.
        ///
        /// Dapper matches a constructor by exact type, and this shape is dictated by the
        /// statement rather than by the domain: a date reached through a LEFT JOIN arrives as
        /// <c>DateTime</c>, and <c>DATEDIFF</c> as <c>Int32</c>. Turning those into a
        /// <c>DateOnly</c> and an enum is the job of this file, next to the driver — the same
        /// lesson the timeline of the M10 taught, and the same way it was learned: by asking
        /// the driver what it was actually sending.
        /// </summary>
        private sealed record MarketRow(
            Guid Code,
            string Plate,
            string Brand,
            string Model,
            string? Version,
            short ModelYear,
            int Status,
            int? DaysInStock,
            decimal PurchasePrice,
            DateTime? PurchaseDate,
            decimal? PurchaseReference,
            decimal? DesiredNetPrice,
            decimal? CurrentReference,
            decimal? PreviousReference,
            decimal? SaleAmount,
            DateTime? SaleDate,
            decimal? SaleReference);

        /// <inheritdoc/>
        public async Task<IReadOnlyList<MarketProposal>> ListMarketProposalsAsync(
            int idTenant,
            DateOnly currentMonth,
            CancellationToken cancellationToken = default)
        {
            var rows = await QueryColumnAsync<MarketProposalRow>(
                new ListMarketProposalsQuery(idTenant, currentMonth), cancellationToken)
                .ConfigureAwait(false);

            return [.. rows.Select(row => new MarketProposal(
                row.VehicleCode,
                row.Plate,
                row.Brand,
                row.Model,
                row.ProspectName,
                row.Amount,
                DateOnly.FromDateTime(row.Date),
                row.CurrentReference))];
        }

        /// <summary>The offer as the driver hands it over. Same reason as <see cref="MarketRow"/>.</summary>
        private sealed record MarketProposalRow(
            Guid VehicleCode,
            string Plate,
            string Brand,
            string Model,
            string ProspectName,
            decimal Amount,
            DateTime Date,
            decimal? CurrentReference);

        private static DateOnly? Day(DateTime? moment) =>
            moment is null ? null : DateOnly.FromDateTime(moment.Value);

        /// <inheritdoc/>
        public Task<IReadOnlyList<Vehicle>> ListBehindFipeAsync(
            DateOnly publishedMonth,
            int limit,
            CancellationToken cancellationToken = default) =>
            QueryAsync(new ListVehiclesBehindFipeQuery(publishedMonth, limit), cancellationToken);

        /// <inheritdoc/>
        public async Task<bool> IdentifierExistsAsync(
            int idTenant,
            string plate,
            string chassis,
            int? ignoreId,
            CancellationToken cancellationToken = default)
        {
            var count = await ExecuteScalarAsync<int>(
                new VehicleIdentifierExistsQuery(idTenant, plate, chassis, ignoreId),
                cancellationToken).ConfigureAwait(false);

            return count > 0;
        }
    }

    /// <summary>Dapper repository for <see cref="VehicleExpense"/>.</summary>
    public class VehicleExpenseRepository(IDapperUnitOfWork unitOfWork)
        : DapperRepository<VehicleExpense>(unitOfWork), IVehicleExpenseRepository
    {
        /// <inheritdoc/>
        public Task<IReadOnlyList<VehicleExpense>> ListByVehicleAsync(
            int idVehicle,
            CancellationToken cancellationToken = default) =>
            QueryAsync(new ListVehicleExpensesQuery(idVehicle), cancellationToken);

        /// <inheritdoc/>
        public async Task<IReadOnlyList<VehicleExpense>> ListByVehiclesAsync(
            IReadOnlyCollection<int> idVehicles,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(idVehicles);

            // An empty IN list is a syntax error in SQL, and asking for the expenses of no
            // vehicle has one obvious answer.
            return idVehicles.Count == 0
                ? []
                : await QueryAsync(new ListExpensesOfVehiclesQuery(idVehicles), cancellationToken)
                    .ConfigureAwait(false);
        }

        /// <inheritdoc/>
        ///
        /// <remarks>
        /// Hides the base <c>GetByCodeAsync</c> on purpose: reading goes through a query
        /// object, which is where <c>SoftDeleteTests</c> checks that every SELECT carries
        /// <c>IsActive = 1</c>. See ADR-0003.
        /// </remarks>
        public new Task<VehicleExpense?> GetByCodeAsync(
            Guid code,
            CancellationToken cancellationToken = default) =>
            QuerySingleAsync(new FindVehicleExpenseByCodeQuery(code), cancellationToken);

        /// <inheritdoc/>
        public async Task<IReadOnlyList<UsedExpenseDescription>> SuggestDescriptionsAsync(
            int idTenant,
            string term,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(term);

            var matches = await QueryAsync(
                new ListExpensesForSuggestionQuery(idTenant, term), cancellationToken)
                .ConfigureAwait(false);

            // One entry per description, filed under the type it was used with most often:
            // somebody who classified "lanterna traseira" as parts nine times out of ten meant
            // parts, and the tenth was a slip.
            return [.. matches
                .GroupBy(e => e.Description, StringComparer.OrdinalIgnoreCase)
                .Select(group => new UsedExpenseDescription(
                    group.First().Description,
                    group.GroupBy(e => e.IdExpenseType)
                        .OrderByDescending(byType => byType.Count())
                        .First().Key,
                    group.Count()))
                .OrderByDescending(suggestion => suggestion.Uses)
                .ThenBy(suggestion => suggestion.Description, StringComparer.OrdinalIgnoreCase)
                .Take(10)];
        }
    }

    /// <summary>Dapper repository for <see cref="ExpenseType"/>.</summary>
    public class ExpenseTypeRepository(IDapperUnitOfWork unitOfWork)
        : DapperRepository<ExpenseType>(unitOfWork), IExpenseTypeRepository
    {
        /// <inheritdoc/>
        public Task<IReadOnlyList<ExpenseType>> ListByTenantAsync(
            int idTenant,
            CancellationToken cancellationToken = default) =>
            QueryAsync(new ListExpenseTypesQuery(idTenant), cancellationToken);

        /// <inheritdoc/>
        public Task<ExpenseType?> GetByCodeAsync(
            int idTenant,
            Guid code,
            CancellationToken cancellationToken = default) =>
            QuerySingleAsync(new FindExpenseTypeByCodeQuery(idTenant, code), cancellationToken);

        /// <inheritdoc/>
        public Task<int> CountExpensesAsync(
            int idExpenseType,
            CancellationToken cancellationToken = default) =>
            ExecuteScalarAsync<int>(new CountExpensesByTypeQuery(idExpenseType), cancellationToken);

    }


    /// <summary>Dapper repository for <see cref="VehiclePhoto"/>.</summary>
    public class VehiclePhotoRepository(IDapperUnitOfWork unitOfWork)
        : DapperRepository<VehiclePhoto>(unitOfWork), IVehiclePhotoRepository
    {
        /// <inheritdoc/>
        public Task<IReadOnlyList<VehiclePhoto>> ListByVehicleAsync(
            int idVehicle,
            CancellationToken cancellationToken = default) =>
            QueryAsync(new Queries.Vehicles.ListVehiclePhotosQuery(idVehicle), cancellationToken);

        /// <inheritdoc/>
        ///
        /// <remarks>
        /// Hides the base <c>GetByCodeAsync</c> on purpose: reading goes through a query
        /// object, which is where <c>SoftDeleteTests</c> checks that every SELECT carries
        /// <c>IsActive = 1</c>. See ADR-0003.
        /// </remarks>
        public new Task<VehiclePhoto?> GetByCodeAsync(
            Guid code,
            CancellationToken cancellationToken = default) =>
            QuerySingleAsync(new FindVehiclePhotoByCodeQuery(code), cancellationToken);

        /// <inheritdoc/>
        public async Task<IReadOnlyList<VehicleGallery>> SummarizeAsync(
            IReadOnlyCollection<int> idVehicles,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(idVehicles);

            // An empty IN list is a syntax error in SQL, and asking about the gallery of no
            // vehicle has one obvious answer.
            if (idVehicles.Count == 0)
            {
                return [];
            }

            var rows = await QueryColumnAsync<GalleryRow>(
                new SummarizeVehicleGalleriesQuery(idVehicles), cancellationToken)
                .ConfigureAwait(false);

            return [.. rows.Select(row =>
                new VehicleGallery(row.IdVehicle, (int)row.PhotoCount, row.CoverStorageKey))];
        }

        /// <summary>
        /// The row as the driver hands it over.
        ///
        /// <c>COUNT(*)</c> comes back as a 64 bit integer, and Dapper matches a constructor by
        /// exact type: a record taking <c>int</c> is simply refused, at runtime, with a message
        /// about a missing parameterless constructor. The narrowing belongs here, next to the
        /// driver, and never in the contract the domain reads — a gallery has an int of photos.
        /// </summary>
        private sealed record GalleryRow(int IdVehicle, long PhotoCount, string? CoverStorageKey);
    }

    /// <summary>Dapper repository for <see cref="VehicleDocument"/>.</summary>
    public class VehicleDocumentRepository(IDapperUnitOfWork unitOfWork)
        : DapperRepository<VehicleDocument>(unitOfWork), IVehicleDocumentRepository
    {
        /// <inheritdoc/>
        public Task<IReadOnlyList<DeletedVehicleDocument>> ListDeletedByTenantAsync(
            int idTenant,
            CancellationToken cancellationToken = default) =>
            QueryColumnAsync<DeletedVehicleDocument>(
                new ListDeletedVehicleDocumentsQuery(idTenant), cancellationToken);

        /// <inheritdoc/>
        public Task<VehicleDocument?> GetByCodeIncludingDeletedAsync(
            Guid code,
            CancellationToken cancellationToken = default) =>
            QuerySingleAsync(
                new FindVehicleDocumentByCodeIncludingDeletedQuery(code), cancellationToken);

        /// <inheritdoc/>
        public Task<IReadOnlyList<VehicleDocument>> ListByVehicleAsync(
            int idVehicle,
            CancellationToken cancellationToken = default) =>
            QueryAsync(new Queries.Vehicles.ListVehicleDocumentsQuery(idVehicle), cancellationToken);

        /// <inheritdoc/>
        ///
        /// <remarks>
        /// Hides the base <c>GetByCodeAsync</c> on purpose: reading goes through a query
        /// object, which is where <c>SoftDeleteTests</c> checks that every SELECT carries
        /// <c>IsActive = 1</c>. See ADR-0003.
        /// </remarks>
        public new Task<VehicleDocument?> GetByCodeAsync(
            Guid code,
            CancellationToken cancellationToken = default) =>
            QuerySingleAsync(new FindVehicleDocumentByCodeQuery(code), cancellationToken);
    }
    /// <summary>Dapper repository for <see cref="VehicleStatusHistory"/>.</summary>
    public class VehicleStatusHistoryRepository(IDapperUnitOfWork unitOfWork)
        : DapperRepository<VehicleStatusHistory>(unitOfWork), IVehicleStatusHistoryRepository
    {
        /// <inheritdoc/>
        public Task<IReadOnlyList<VehicleStatusHistory>> ListByVehicleAsync(
            int idVehicle,
            CancellationToken cancellationToken = default) =>
            QueryAsync(new ListVehicleStatusHistoryQuery(idVehicle), cancellationToken);
    }
}
