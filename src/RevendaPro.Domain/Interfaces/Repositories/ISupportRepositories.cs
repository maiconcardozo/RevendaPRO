using Foundation.Domain.Interfaces.Repositories;
using RevendaPro.Domain.Entities;

namespace RevendaPro.Domain.Interfaces.Repositories
{
    public interface ITenantRepository : IDapperRepository<Tenant>
    {
        Task<Tenant?> GetFirstAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// A revenda de quem está logado, pelo Id do token. Passa por consulta própria, com o
        /// filtro de exclusão lógica, como toda leitura deste sistema.
        /// </summary>
        /// <param name="id">Id interno da revenda.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A revenda, ou nulo.</returns>
        Task<Tenant?> FindAsync(int id, CancellationToken cancellationToken = default);
    }

    public interface IRefreshTokenRepository : IDapperRepository<RefreshToken>
    {
        Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default);

        /// <summary>Buffers the revocation of every active token of the user. Runs on Commit.</summary>
        void RevokeAllByUser(int idUser, string actor);
    }

    public interface IAuditLogRepository : IDapperRepository<AuditLog>;
}
