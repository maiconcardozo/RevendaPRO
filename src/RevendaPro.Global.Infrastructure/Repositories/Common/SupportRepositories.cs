using Foundation.Dapper.Repositories;
using Foundation.Domain.Interfaces.UnitOfWork;
using RevendaPro.Global.Domain.Entities;
using RevendaPro.Global.Domain.Interfaces.Repositories;
using RevendaPro.Global.Infrastructure.Queries.Common;

namespace RevendaPro.Global.Infrastructure.Repositories.Common
{
    /// <summary>Dapper repository for <see cref="Tenant"/>.</summary>
    public class TenantRepository(IDapperUnitOfWork unitOfWork)
        : DapperRepository<Tenant>(unitOfWork), ITenantRepository
    {
        /// <inheritdoc/>
        public Task<Tenant?> GetFirstAsync(CancellationToken cancellationToken = default) =>
            QuerySingleAsync(new FindFirstTenantQuery(), cancellationToken);
    }

    /// <summary>Dapper repository for <see cref="RefreshToken"/>.</summary>
    public class RefreshTokenRepository(IDapperUnitOfWork unitOfWork)
        : DapperRepository<RefreshToken>(unitOfWork), IRefreshTokenRepository
    {
        /// <inheritdoc/>
        public Task<RefreshToken?> GetByHashAsync(
            string tokenHash,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);

            return QuerySingleAsync(new FindRefreshTokenByHashQuery(tokenHash), cancellationToken);
        }

        /// <inheritdoc/>
        public void RevokeAllByUser(int userId, string actor)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(actor);

            Enqueue(new RevokeUserRefreshTokensQuery(userId, actor));
        }
    }

    /// <summary>Dapper repository for <see cref="AuditLog"/>. Conventional CRUD is enough.</summary>
    public class AuditLogRepository(IDapperUnitOfWork unitOfWork)
        : DapperRepository<AuditLog>(unitOfWork), IAuditLogRepository;
}
