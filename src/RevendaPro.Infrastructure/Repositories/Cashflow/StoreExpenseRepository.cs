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

    /// <summary>Dapper repository for <see cref="SaleReceipt"/>.</summary>
    public class SaleReceiptRepository(IDapperUnitOfWork unitOfWork)
        : DapperRepository<SaleReceipt>(unitOfWork), ISaleReceiptRepository
    {
        /// <inheritdoc/>
        public Task<IReadOnlyList<SaleReceipt>> ListBySaleAsync(
            int idSale,
            CancellationToken cancellationToken = default) =>
            QueryAsync(new ListReceiptsOfSaleQuery(idSale), cancellationToken);

        /// <inheritdoc/>
        public Task<IReadOnlyList<SaleReceipt>> ListBySalesAsync(
            IReadOnlyCollection<int> idSales,
            CancellationToken cancellationToken = default) =>
            idSales.Count == 0
                ? Task.FromResult<IReadOnlyList<SaleReceipt>>([])
                : QueryAsync(new ListReceiptsOfSalesQuery(idSales), cancellationToken);

        /// <inheritdoc/>
        public Task<SaleReceipt?> FindAsync(
            int idTenant,
            Guid code,
            CancellationToken cancellationToken = default) =>
            QuerySingleAsync(new FindReceiptByCodeQuery(idTenant, code), cancellationToken);
    }
}
