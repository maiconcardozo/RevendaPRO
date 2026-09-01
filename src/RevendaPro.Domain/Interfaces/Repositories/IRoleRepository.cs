using Foundation.Domain.Interfaces.Repositories;
using RevendaPro.Domain.Entities;

namespace RevendaPro.Domain.Interfaces.Repositories
{
    public interface IRoleRepository : IDapperRepository<Role>
    {
        Task<Role?> GetByNameAsync(int tenantId, string name, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<Role>> ListByTenantAsync(int tenantId, CancellationToken cancellationToken = default);

        Task<bool> NameExistsAsync(
            int tenantId,
            string name,
            int? ignoreId,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<int>> GetScreenIdsAsync(int roleId, CancellationToken cancellationToken = default);

        /// <summary>Buffers the replacement of the role screens. Runs on Commit.</summary>
        void ReplaceScreens(int roleId, IReadOnlyCollection<int> screenIds, string actor);

        /// <summary>Buffers granting one screen, used when a new screen is synchronized.</summary>
        void GrantScreen(int roleId, int screenId, string actor);
    }
}
