using Foundation.Dapper.Repositories;
using Foundation.Domain.Interfaces.UnitOfWork;
using RevendaPro.Global.Domain.Entities;
using RevendaPro.Global.Domain.Interfaces.Repositories;
using RevendaPro.Global.Infrastructure.Queries.Users;

namespace RevendaPro.Global.Infrastructure.Repositories.Users
{
    /// <summary>
    /// Dapper repository for <see cref="User"/>.
    ///
    /// Inherits the conventional CRUD and the query object helpers from Foundation, and
    /// writes SQL only for what falls outside them. Reads run at once; writes are buffered
    /// by the unit of work and reach the database on Commit. See ADR-0003.
    /// </summary>
    public class UserRepository(IDapperUnitOfWork unitOfWork)
        : DapperRepository<User>(unitOfWork), IUserRepository
    {
        /// <inheritdoc/>
        public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(email);

            return QuerySingleAsync(
                new FindUserByEmailQuery(email.Trim().ToLowerInvariant()), cancellationToken);
        }

        /// <inheritdoc/>
        public Task<IReadOnlyList<User>> ListByTenantAsync(
            int tenantId,
            string? search,
            CancellationToken cancellationToken = default) =>
            QueryAsync(new ListUsersByTenantQuery(tenantId, search), cancellationToken);

        /// <inheritdoc/>
        public async Task<bool> EmailExistsAsync(
            int tenantId,
            string email,
            int? ignoreId,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(email);

            var query = new UserEmailExistsQuery(tenantId, email.Trim().ToLowerInvariant(), ignoreId);

            return await ExecuteScalarAsync<long>(query, cancellationToken).ConfigureAwait(false) > 0;
        }

        /// <inheritdoc/>
        public async Task<int> CountByRoleAsync(int roleId, CancellationToken cancellationToken = default) =>
            (int)await ExecuteScalarAsync<long>(new CountUsersByRoleQuery(roleId), cancellationToken)
                .ConfigureAwait(false);

        /// <inheritdoc/>
        public Task<IReadOnlyList<string>> GetScreenKeysAsync(
            int userId,
            CancellationToken cancellationToken = default) =>
            QueryColumnAsync<string>(new ListScreenKeysByUserQuery(userId), cancellationToken);

        /// <inheritdoc/>
        public Task<IReadOnlyList<int>> GetRoleIdsAsync(
            int userId,
            CancellationToken cancellationToken = default) =>
            QueryColumnAsync<int>(new ListRoleIdsByUserQuery(userId), cancellationToken);

        /// <inheritdoc/>
        public void ReplaceRoles(int userId, IReadOnlyCollection<int> roleIds, string actor)
        {
            ArgumentNullException.ThrowIfNull(roleIds);
            ArgumentException.ThrowIfNullOrWhiteSpace(actor);

            // Clearing first and granting afterwards keeps both statements in the same
            // buffered batch, so no committed state ever leaves the user without a role.
            Enqueue(new ClearUserRolesQuery(userId, actor));

            foreach (var roleId in roleIds.Distinct())
            {
                Enqueue(new GrantRoleToUserQuery(userId, roleId, actor));
            }
        }
    }
}
