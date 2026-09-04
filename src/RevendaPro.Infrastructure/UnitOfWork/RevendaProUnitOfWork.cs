using Foundation.Dapper;
using Foundation.Domain.Interfaces.Data;
using RevendaPro.Domain.Interfaces;
using RevendaPro.Domain.Interfaces.Repositories;

namespace RevendaPro.Infrastructure.UnitOfWork
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
        Func<Foundation.Domain.Interfaces.UnitOfWork.IDapperUnitOfWork, IAuditLogRepository> auditLogRepositoryFactory,
        Func<Foundation.Domain.Interfaces.UnitOfWork.IDapperUnitOfWork, IVehicleRepository> vehicleRepositoryFactory,
        Func<Foundation.Domain.Interfaces.UnitOfWork.IDapperUnitOfWork, IVehicleExpenseRepository> vehicleExpenseRepositoryFactory,
        Func<Foundation.Domain.Interfaces.UnitOfWork.IDapperUnitOfWork, IExpenseTypeRepository> expenseTypeRepositoryFactory,
        Func<Foundation.Domain.Interfaces.UnitOfWork.IDapperUnitOfWork, IVehiclePhotoRepository> vehiclePhotoRepositoryFactory,
        Func<Foundation.Domain.Interfaces.UnitOfWork.IDapperUnitOfWork, IVehicleDocumentRepository> vehicleDocumentRepositoryFactory,
        Func<Foundation.Domain.Interfaces.UnitOfWork.IDapperUnitOfWork, IVehicleStatusHistoryRepository> vehicleStatusHistoryRepositoryFactory,
        Func<Foundation.Domain.Interfaces.UnitOfWork.IDapperUnitOfWork, IProposalRepository> proposalRepositoryFactory,
        Func<Foundation.Domain.Interfaces.UnitOfWork.IDapperUnitOfWork, ISaleRepository> saleRepositoryFactory,
        Func<Foundation.Domain.Interfaces.UnitOfWork.IDapperUnitOfWork, IFipeQuoteRepository> fipeQuoteRepositoryFactory,
        Func<Foundation.Domain.Interfaces.UnitOfWork.IDapperUnitOfWork, IYardRepository> yardRepositoryFactory)
        : DapperUnitOfWork(connectionFactory), IUnitOfWork
    {
        private IUserRepository? _userRepository;
        private IRoleRepository? _roleRepository;
        private IScreenRepository? _screenRepository;
        private ITenantRepository? _tenantRepository;
        private IRefreshTokenRepository? _refreshTokenRepository;
        private IAuditLogRepository? _auditLogRepository;
        private IVehicleRepository? _vehicleRepository;
        private IVehicleExpenseRepository? _vehicleExpenseRepository;
        private IExpenseTypeRepository? _expenseTypeRepository;
        private IVehiclePhotoRepository? _vehiclePhotoRepository;
        private IVehicleDocumentRepository? _vehicleDocumentRepository;
        private IVehicleStatusHistoryRepository? _vehicleStatusHistoryRepository;
        private IProposalRepository? _proposalRepository;
        private ISaleRepository? _saleRepository;
        private IFipeQuoteRepository? _fipeQuoteRepository;
        private IYardRepository? _yardRepository;

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

        /// <inheritdoc/>
        public IVehicleRepository VehicleRepository =>
            _vehicleRepository ??= vehicleRepositoryFactory(this);

        /// <inheritdoc/>
        public IVehicleExpenseRepository VehicleExpenseRepository =>
            _vehicleExpenseRepository ??= vehicleExpenseRepositoryFactory(this);

        /// <inheritdoc/>
        public IExpenseTypeRepository ExpenseTypeRepository =>
            _expenseTypeRepository ??= expenseTypeRepositoryFactory(this);

        /// <inheritdoc/>
        public IVehiclePhotoRepository VehiclePhotoRepository =>
            _vehiclePhotoRepository ??= vehiclePhotoRepositoryFactory(this);

        /// <inheritdoc/>
        public IVehicleDocumentRepository VehicleDocumentRepository =>
            _vehicleDocumentRepository ??= vehicleDocumentRepositoryFactory(this);

        /// <inheritdoc/>
        public IVehicleStatusHistoryRepository VehicleStatusHistoryRepository =>
            _vehicleStatusHistoryRepository ??= vehicleStatusHistoryRepositoryFactory(this);

        /// <inheritdoc/>
        public IProposalRepository ProposalRepository =>
            _proposalRepository ??= proposalRepositoryFactory(this);

        /// <inheritdoc/>
        public ISaleRepository SaleRepository =>
            _saleRepository ??= saleRepositoryFactory(this);

        /// <inheritdoc/>
        public IFipeQuoteRepository FipeQuoteRepository =>
            _fipeQuoteRepository ??= fipeQuoteRepositoryFactory(this);

        /// <inheritdoc/>
        public IYardRepository YardRepository =>
            _yardRepository ??= yardRepositoryFactory(this);
    }
}
