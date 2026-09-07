using Foundation.Dapper.Repositories;
using Foundation.Domain.Interfaces.UnitOfWork;
using RevendaPro.Domain.Entities;
using RevendaPro.Domain.Interfaces.Repositories;
using RevendaPro.Infrastructure.Queries.Suppliers;

namespace RevendaPro.Infrastructure.Repositories.Suppliers
{
    /// <summary>Dapper repository for <see cref="Supplier"/>.</summary>
    public class SupplierRepository(IDapperUnitOfWork unitOfWork)
        : DapperRepository<Supplier>(unitOfWork), ISupplierRepository
    {
        /// <inheritdoc/>
        public Task<IReadOnlyList<Supplier>> ListByTenantAsync(
            int idTenant,
            CancellationToken cancellationToken = default) =>
            QueryAsync(new ListSuppliersByTenantQuery(idTenant), cancellationToken);

        /// <inheritdoc/>
        public Task<Supplier?> GetByCodeAsync(
            int idTenant,
            Guid code,
            CancellationToken cancellationToken = default) =>
            QuerySingleAsync(new FindSupplierByCodeQuery(idTenant, code), cancellationToken);

        /// <inheritdoc/>
        public Task<int> CountExpensesAsync(
            int idSupplier,
            CancellationToken cancellationToken = default) =>
            ExecuteScalarAsync<int>(new CountExpensesOfSupplierQuery(idSupplier), cancellationToken);

        /// <inheritdoc/>
        public async Task<bool> NameExistsAsync(
            int idTenant,
            string name,
            int? ignoreId,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);

            var count = await ExecuteScalarAsync<int>(
                new SupplierNameExistsQuery(idTenant, name.Trim(), ignoreId), cancellationToken)
                .ConfigureAwait(false);

            return count > 0;
        }
    }

    /// <summary>Dapper repository for <see cref="SupplierSegment"/>.</summary>
    public class SupplierSegmentRepository(IDapperUnitOfWork unitOfWork)
        : DapperRepository<SupplierSegment>(unitOfWork), ISupplierSegmentRepository
    {
        /// <inheritdoc/>
        public Task<IReadOnlyList<SupplierSegment>> ListByTenantAsync(
            int idTenant,
            CancellationToken cancellationToken = default) =>
            QueryAsync(new ListSupplierSegmentsByTenantQuery(idTenant), cancellationToken);

        /// <inheritdoc/>
        public Task<SupplierSegment?> GetByCodeAsync(
            int idTenant,
            Guid code,
            CancellationToken cancellationToken = default) =>
            QuerySingleAsync(new FindSupplierSegmentByCodeQuery(idTenant, code), cancellationToken);

        /// <inheritdoc/>
        public Task<int> CountSuppliersAsync(
            int idSupplierSegment,
            CancellationToken cancellationToken = default) =>
            ExecuteScalarAsync<int>(new CountSuppliersInSegmentQuery(idSupplierSegment), cancellationToken);

        /// <inheritdoc/>
        public async Task<bool> NameExistsAsync(
            int idTenant,
            string name,
            int? ignoreId,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);

            var count = await ExecuteScalarAsync<int>(
                new SupplierSegmentNameExistsQuery(idTenant, name.Trim(), ignoreId), cancellationToken)
                .ConfigureAwait(false);

            return count > 0;
        }
    }
}
