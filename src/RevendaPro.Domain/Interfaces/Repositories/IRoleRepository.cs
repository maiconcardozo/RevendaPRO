using Foundation.Domain.Interfaces.Repositories;
using RevendaPro.Domain.Entities;

namespace RevendaPro.Domain.Interfaces.Repositories
{
    public interface IRoleRepository : IDapperRepository<Role>
    {
        Task<Role?> GetByNameAsync(int idTenant, string name, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<Role>> ListByTenantAsync(int idTenant, CancellationToken cancellationToken = default);

        Task<bool> NameExistsAsync(
            int idTenant,
            string name,
            int? ignoreId,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<int>> GetScreenIdsAsync(int idRole, CancellationToken cancellationToken = default);

        /// <summary>Buffers the replacement of the role screens. Runs on Commit.</summary>
        void ReplaceScreens(int idRole, IReadOnlyCollection<int> screenIds, string actor);

        /// <summary>Buffers granting one screen, used when a new screen is synchronized.</summary>
        void GrantScreen(int idRole, int idScreen, string actor);
    }
}
