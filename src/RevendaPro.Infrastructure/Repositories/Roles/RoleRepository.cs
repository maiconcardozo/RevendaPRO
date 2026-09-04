using Foundation.Dapper.Repositories;
using Foundation.Domain.Interfaces.UnitOfWork;
using RevendaPro.Domain.Entities;
using RevendaPro.Domain.Interfaces.Repositories;
using RevendaPro.Infrastructure.Queries.Roles;

namespace RevendaPro.Infrastructure.Repositories.Roles
{
    /// <summary>Dapper repository for <see cref="Role"/>.</summary>
    public class RoleRepository(IDapperUnitOfWork unitOfWork)
        : DapperRepository<Role>(unitOfWork), IRoleRepository
    {
        /// <inheritdoc/>
        public Task<Role?> GetByCodeAsync(
            int idTenant,
            Guid code,
            CancellationToken cancellationToken = default) =>
            QuerySingleAsync(new FindRoleByCodeQuery(idTenant, code), cancellationToken);

        /// <inheritdoc/>
        public Task<Role?> GetByNameAsync(
            int idTenant,
            string name,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);

            return QuerySingleAsync(new FindRoleByNameQuery(idTenant, name.Trim()), cancellationToken);
        }

        /// <inheritdoc/>
        public Task<IReadOnlyList<Role>> ListByTenantAsync(
            int idTenant,
            CancellationToken cancellationToken = default) =>
            QueryAsync(new ListRolesByTenantQuery(idTenant), cancellationToken);

        /// <inheritdoc/>
        public async Task<bool> NameExistsAsync(
            int idTenant,
            string name,
            int? ignoreId,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);

            var query = new RoleNameExistsQuery(idTenant, name.Trim(), ignoreId);

            return await ExecuteScalarAsync<long>(query, cancellationToken).ConfigureAwait(false) > 0;
        }

        /// <inheritdoc/>
        public Task<IReadOnlyList<int>> GetScreenIdsAsync(
            int idRole,
            CancellationToken cancellationToken = default) =>
            QueryColumnAsync<int>(new ListScreenIdsByRoleQuery(idRole), cancellationToken);

        /// <inheritdoc/>
        public void ReplaceScreens(int idRole, IReadOnlyCollection<int> screenIds, string actor)
        {
            ArgumentNullException.ThrowIfNull(screenIds);
            ArgumentException.ThrowIfNullOrWhiteSpace(actor);

            Enqueue(new ClearRoleScreensQuery(idRole, actor));

            foreach (var idScreen in screenIds.Distinct())
            {
                Enqueue(new GrantScreenToRoleQuery(idRole, idScreen, actor));
            }
        }

        /// <inheritdoc/>
        public void GrantScreen(int idRole, int idScreen, string actor)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(actor);

            Enqueue(new GrantScreenToRoleQuery(idRole, idScreen, actor));
        }
    }
}
