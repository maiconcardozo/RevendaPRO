using Foundation.Dapper.Repositories;
using Foundation.Domain.Interfaces.UnitOfWork;
using RevendaPro.Domain.Entities;
using RevendaPro.Domain.Interfaces.Repositories;
using RevendaPro.Infrastructure.Queries.Users;

namespace RevendaPro.Infrastructure.Repositories.Users
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
            int idTenant,
            string? search,
            bool includeDeleted,
            CancellationToken cancellationToken = default) =>
            QueryAsync(
                new ListUsersByTenantQuery(idTenant, search, includeDeleted), cancellationToken);

        /// <inheritdoc/>
        public Task<User?> GetByCodeIncludingDeletedAsync(
            Guid code,
            CancellationToken cancellationToken = default) =>
            QuerySingleAsync(new FindUserByCodeIncludingDeletedQuery(code), cancellationToken);

        /// <inheritdoc/>
        public async Task<bool> EmailExistsAsync(
            int idTenant,
            string email,
            int? ignoreId,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(email);

            var query = new UserEmailExistsQuery(idTenant, email.Trim().ToLowerInvariant(), ignoreId);

            return await ExecuteScalarAsync<long>(query, cancellationToken).ConfigureAwait(false) > 0;
        }

        /// <inheritdoc/>
        public async Task<int> CountByRoleAsync(int idRole, CancellationToken cancellationToken = default) =>
            (int)await ExecuteScalarAsync<long>(new CountUsersByRoleQuery(idRole), cancellationToken)
                .ConfigureAwait(false);

        /// <inheritdoc/>
        public Task<IReadOnlyList<string>> GetScreenKeysAsync(
            int idUser,
            CancellationToken cancellationToken = default) =>
            QueryColumnAsync<string>(new ListScreenKeysByUserQuery(idUser), cancellationToken);

        /// <inheritdoc/>
        public Task<IReadOnlyList<int>> GetRoleIdsAsync(
            int idUser,
            CancellationToken cancellationToken = default) =>
            QueryColumnAsync<int>(new ListRoleIdsByUserQuery(idUser), cancellationToken);

        /// <inheritdoc/>
        public void ReplaceRoles(int idUser, IReadOnlyCollection<int> roleIds, string actor)
        {
            ArgumentNullException.ThrowIfNull(roleIds);
            ArgumentException.ThrowIfNullOrWhiteSpace(actor);

            // Clearing first and granting afterwards keeps both statements in the same
            // buffered batch, so no committed state ever leaves the user without a role.
            Enqueue(new ClearUserRolesQuery(idUser, actor));

            foreach (var idRole in roleIds.Distinct())
            {
                Enqueue(new GrantRoleToUserQuery(idUser, idRole, actor));
            }
        }
    }
}
