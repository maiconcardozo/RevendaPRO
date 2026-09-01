using Foundation.Dapper.Repositories;
using Foundation.Domain.Interfaces.UnitOfWork;
using RevendaPro.Global.Domain.Entities;
using RevendaPro.Global.Domain.Interfaces.Repositories;
using RevendaPro.Global.Infrastructure.Queries.Roles;

namespace RevendaPro.Global.Infrastructure.Repositories.Roles
{
    /// <summary>Dapper repository for <see cref="Role"/>.</summary>
    public class RoleRepository(IDapperUnitOfWork unitOfWork)
        : DapperRepository<Role>(unitOfWork), IRoleRepository
    {
        /// <inheritdoc/>
        public Task<Role?> GetByNameAsync(
            int tenantId,
            string name,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);

            return QuerySingleAsync(new FindRoleByNameQuery(tenantId, name.Trim()), cancellationToken);
        }

        /// <inheritdoc/>
        public Task<IReadOnlyList<Role>> ListByTenantAsync(
            int tenantId,
            CancellationToken cancellationToken = default) =>
            QueryAsync(new ListRolesByTenantQuery(tenantId), cancellationToken);

        /// <inheritdoc/>
        public async Task<bool> NameExistsAsync(
            int tenantId,
            string name,
            int? ignoreId,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);

            var query = new RoleNameExistsQuery(tenantId, name.Trim(), ignoreId);

            return await ExecuteScalarAsync<long>(query, cancellationToken).ConfigureAwait(false) > 0;
        }

        /// <inheritdoc/>
        public Task<IReadOnlyList<int>> GetScreenIdsAsync(
            int roleId,
            CancellationToken cancellationToken = default) =>
            QueryColumnAsync<int>(new ListScreenIdsByRoleQuery(roleId), cancellationToken);

        /// <inheritdoc/>
        public void ReplaceScreens(int roleId, IReadOnlyCollection<int> screenIds, string actor)
        {
            ArgumentNullException.ThrowIfNull(screenIds);
            ArgumentException.ThrowIfNullOrWhiteSpace(actor);

            Enqueue(new ClearRoleScreensQuery(roleId, actor));

            foreach (var screenId in screenIds.Distinct())
            {
                Enqueue(new GrantScreenToRoleQuery(roleId, screenId, actor));
            }
        }

        /// <inheritdoc/>
        public void GrantScreen(int roleId, int screenId, string actor)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(actor);

            Enqueue(new GrantScreenToRoleQuery(roleId, screenId, actor));
        }
    }
}
