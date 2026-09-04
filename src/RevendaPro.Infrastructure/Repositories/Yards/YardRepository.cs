using Foundation.Dapper.Repositories;
using Foundation.Domain.Interfaces.UnitOfWork;
using RevendaPro.Domain.Entities;
using RevendaPro.Domain.Interfaces.Repositories;
using RevendaPro.Infrastructure.Queries.Yards;

namespace RevendaPro.Infrastructure.Repositories.Yards
{
    /// <summary>Dapper repository for <see cref="Yard"/>.</summary>
    public class YardRepository(IDapperUnitOfWork unitOfWork)
        : DapperRepository<Yard>(unitOfWork), IYardRepository
    {
        /// <inheritdoc/>
        public Task<IReadOnlyList<Yard>> ListByTenantAsync(
            int idTenant,
            CancellationToken cancellationToken = default) =>
            QueryAsync(new ListYardsByTenantQuery(idTenant), cancellationToken);

        /// <inheritdoc/>
        public Task<Yard?> GetByCodeAsync(
            int idTenant,
            Guid code,
            CancellationToken cancellationToken = default) =>
            QuerySingleAsync(new FindYardByCodeQuery(idTenant, code), cancellationToken);

        /// <inheritdoc/>
        public Task<int> CountVehiclesAsync(
            int idYard,
            CancellationToken cancellationToken = default) =>
            ExecuteScalarAsync<int>(new CountVehiclesInYardQuery(idYard), cancellationToken);

        /// <inheritdoc/>
        public async Task<bool> NameExistsAsync(
            int idTenant,
            string name,
            int? ignoreId,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);

            var count = await ExecuteScalarAsync<int>(
                new YardNameExistsQuery(idTenant, name.Trim(), ignoreId), cancellationToken)
                .ConfigureAwait(false);

            return count > 0;
        }
    }
}
