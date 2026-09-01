using Foundation.Dapper;
using Foundation.Domain.Interfaces.Data;
using RevendaPro.Global.Domain.Interfaces;
using RevendaPro.Global.Domain.Interfaces.Repositories;

namespace RevendaPro.Global.Infrastructure.UnitOfWork
{
    /// <summary>
    /// Unit of work of this project.
    ///
    /// Everything transactional - the write buffer, Commit, Rollback, ExecuteInTransaction -
    /// comes from Foundation's <see cref="DapperUnitOfWork"/>. This class only exposes the
    /// repositories, exactly like AuthUnitOfWork does in source/Global/Authentication, and
    /// creates each one lazily so a request that never touches a table never builds its
    /// repository.
    /// </summary>
    public class RevendaProUnitOfWork(
        ISqlConnectionFactory connectionFactory,
        Func<Foundation.Domain.Interfaces.UnitOfWork.IDapperUnitOfWork, IUserRepository> userRepositoryFactory,
        Func<Foundation.Domain.Interfaces.UnitOfWork.IDapperUnitOfWork, IRoleRepository> roleRepositoryFactory,
        Func<Foundation.Domain.Interfaces.UnitOfWork.IDapperUnitOfWork, IScreenRepository> screenRepositoryFactory,
        Func<Foundation.Domain.Interfaces.UnitOfWork.IDapperUnitOfWork, ITenantRepository> tenantRepositoryFactory,
        Func<Foundation.Domain.Interfaces.UnitOfWork.IDapperUnitOfWork, IRefreshTokenRepository> refreshTokenRepositoryFactory,
        Func<Foundation.Domain.Interfaces.UnitOfWork.IDapperUnitOfWork, IAuditLogRepository> auditLogRepositoryFactory)
        : DapperUnitOfWork(connectionFactory), IUnitOfWork
    {
        private IUserRepository? _userRepository;
        private IRoleRepository? _roleRepository;
        private IScreenRepository? _screenRepository;
        private ITenantRepository? _tenantRepository;
        private IRefreshTokenRepository? _refreshTokenRepository;
        private IAuditLogRepository? _auditLogRepository;

        /// <inheritdoc/>
        public IUserRepository UserRepository =>
            _userRepository ??= userRepositoryFactory(this);

        /// <inheritdoc/>
        public IRoleRepository RoleRepository =>
            _roleRepository ??= roleRepositoryFactory(this);

        /// <inheritdoc/>
        public IScreenRepository ScreenRepository =>
            _screenRepository ??= screenRepositoryFactory(this);

        /// <inheritdoc/>
        public ITenantRepository TenantRepository =>
            _tenantRepository ??= tenantRepositoryFactory(this);

        /// <inheritdoc/>
        public IRefreshTokenRepository RefreshTokenRepository =>
            _refreshTokenRepository ??= refreshTokenRepositoryFactory(this);

        /// <inheritdoc/>
        public IAuditLogRepository AuditLogRepository =>
            _auditLogRepository ??= auditLogRepositoryFactory(this);
    }
}
