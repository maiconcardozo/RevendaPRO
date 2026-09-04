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
        /// <summary>
        /// Finds a role of a dealership by public code.
        ///
        /// <b>Toda leitura por código carrega a empresa</b>, pelo mesmo motivo do usuário.
        /// </summary>
        /// <param name="idTenant">Owning tenant.</param>
        /// <param name="code">Public identifier.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The role, or null.</returns>
        Task<Role?> GetByCodeAsync(
            int idTenant,
            Guid code,
            CancellationToken cancellationToken = default);

        void ReplaceScreens(int idRole, IReadOnlyCollection<int> screenIds, string actor);

        /// <summary>Buffers granting one screen, used when a new screen is synchronized.</summary>
        void GrantScreen(int idRole, int idScreen, string actor);
    }
}
