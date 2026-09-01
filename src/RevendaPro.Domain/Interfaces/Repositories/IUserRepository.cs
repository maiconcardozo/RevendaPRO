using Foundation.Domain.Interfaces.Repositories;
using RevendaPro.Domain.Entities;

namespace RevendaPro.Domain.Interfaces.Repositories
{
    /// <summary>
    /// Extends the conventional CRUD of <see cref="IDapperRepository{TEntity}"/> with what
    /// only this project knows how to ask. There is no IQueryable here on purpose: Entity
    /// Framework Core is used only for migrations and mappings. See ADR-0003.
    /// </summary>
    public interface IUserRepository : IDapperRepository<User>
    {
        Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

        /// <param name="includeDeleted">
        /// Brings rows that were logically deleted, so the screen can offer them back. Off by
        /// default: a deleted person stays out of every other reading.
        /// </param>
        Task<IReadOnlyList<User>> ListByTenantAsync(
            int idTenant,
            string? search,
            bool includeDeleted,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Finds a user by code even when deleted. Only the restore path uses it; everything
        /// else goes through the conventional read, which leaves deleted rows out.
        /// </summary>
        Task<User?> GetByCodeIncludingDeletedAsync(
            Guid code,
            CancellationToken cancellationToken = default);

        Task<bool> EmailExistsAsync(
            int idTenant,
            string email,
            int? ignoreId,
            CancellationToken cancellationToken = default);

        Task<int> CountByRoleAsync(int idRole, CancellationToken cancellationToken = default);

        /// <summary>Screen keys the user can reach: the union of the screens of their roles.</summary>
        Task<IReadOnlyList<string>> GetScreenKeysAsync(
            int idUser,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<int>> GetRoleIdsAsync(int idUser, CancellationToken cancellationToken = default);

        /// <summary>Buffers the replacement of the user roles. Runs on Commit.</summary>
        void ReplaceRoles(int idUser, IReadOnlyCollection<int> roleIds, string actor);
    }
}
