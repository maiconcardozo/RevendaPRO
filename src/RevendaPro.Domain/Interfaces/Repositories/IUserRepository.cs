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

        Task<IReadOnlyList<User>> ListByTenantAsync(
            int tenantId,
            string? search,
            CancellationToken cancellationToken = default);

        Task<bool> EmailExistsAsync(
            int tenantId,
            string email,
            int? ignoreId,
            CancellationToken cancellationToken = default);

        Task<int> CountByRoleAsync(int roleId, CancellationToken cancellationToken = default);

        /// <summary>Screen keys the user can reach: the union of the screens of their roles.</summary>
        Task<IReadOnlyList<string>> GetScreenKeysAsync(
            int userId,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<int>> GetRoleIdsAsync(int userId, CancellationToken cancellationToken = default);

        /// <summary>Buffers the replacement of the user roles. Runs on Commit.</summary>
        void ReplaceRoles(int userId, IReadOnlyCollection<int> roleIds, string actor);
    }
}
