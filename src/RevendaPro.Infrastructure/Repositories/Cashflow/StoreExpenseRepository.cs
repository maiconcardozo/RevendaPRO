using Foundation.Dapper.Repositories;
using Foundation.Domain.Interfaces.UnitOfWork;
using RevendaPro.Domain.Entities;
using RevendaPro.Domain.Interfaces.Repositories;
using RevendaPro.Infrastructure.Queries.Cashflow;

namespace RevendaPro.Infrastructure.Repositories.Cashflow
{
    /// <summary>Dapper repository for <see cref="StoreExpense"/>.</summary>
    public class StoreExpenseRepository(IDapperUnitOfWork unitOfWork)
        : DapperRepository<StoreExpense>(unitOfWork), IStoreExpenseRepository
    {
        /// <inheritdoc/>
        public Task<IReadOnlyList<StoreExpense>> ListByTenantAsync(
            int idTenant,
            DateOnly? from,
            DateOnly? to,
            CancellationToken cancellationToken = default) =>
            QueryAsync(new ListStoreExpensesQuery(idTenant, from, to), cancellationToken);

        /// <inheritdoc/>
        public Task<StoreExpense?> GetByCodeAsync(
            int idTenant,
            Guid code,
            CancellationToken cancellationToken = default) =>
            QuerySingleAsync(new FindStoreExpenseByCodeQuery(idTenant, code), cancellationToken);

        /// <inheritdoc/>
        public Task<int> CountByExpenseTypeAsync(
            int idExpenseType,
            CancellationToken cancellationToken = default) =>
            ExecuteScalarAsync<int>(new CountStoreExpensesOfTypeQuery(idExpenseType), cancellationToken);

        /// <inheritdoc/>
        public Task<int> CountBySupplierAsync(
            int idSupplier,
            CancellationToken cancellationToken = default) =>
            ExecuteScalarAsync<int>(new CountStoreExpensesOfSupplierQuery(idSupplier), cancellationToken);
    }
}
