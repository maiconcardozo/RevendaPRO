using Foundation.Dapper.Repositories;
using Foundation.Domain.Interfaces.UnitOfWork;
using RevendaPro.Domain.Entities;
using RevendaPro.Domain.Enums;
using RevendaPro.Domain.Interfaces.Repositories;
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
            CancellationToken cancellationToken = default) =>
            QueryAsync(new ListVehiclesQuery(idTenant, search, status, origin), cancellationToken);

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
        public Task<VehicleExpense?> GetByCodeAsync(
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
