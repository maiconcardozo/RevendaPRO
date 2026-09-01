using Foundation.Domain.Interfaces.Repositories;
using RevendaPro.Domain.Entities;

namespace RevendaPro.Domain.Interfaces.Repositories
{
    public interface ITenantRepository : IDapperRepository<Tenant>
    {
        Task<Tenant?> GetFirstAsync(CancellationToken cancellationToken = default);
    }

    public interface IRefreshTokenRepository : IDapperRepository<RefreshToken>
    {
        Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default);

        /// <summary>Buffers the revocation of every active token of the user. Runs on Commit.</summary>
        void RevokeAllByUser(int idUser, string actor);
    }

    public interface IAuditLogRepository : IDapperRepository<AuditLog>;
}
