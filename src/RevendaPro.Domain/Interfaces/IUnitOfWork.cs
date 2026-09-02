using Foundation.Domain.Interfaces.UnitOfWork;
using RevendaPro.Domain.Interfaces.Repositories;

namespace RevendaPro.Domain.Interfaces
{
    /// <summary>
    /// Coordinates the repositories and the database transaction.
    ///
    /// Extends <see cref="IDapperUnitOfWork"/> from Foundation.Base, which in turn extends
    /// <see cref="IBaseUnitOfWork"/>. Everything transactional - buffering, Commit, Rollback,
    /// ExecuteInTransaction - comes from the package; this interface only names the
    /// repositories of this project, exactly like AuthUnitOfWork does in
    /// source/Global/Authentication.
    ///
    /// Writes are buffered and only reach the database on Commit, the same promise the
    /// Entity Framework unit of work makes. Here Entity Framework is used only to generate
    /// migrations and map tables. See ADR-0003.
    /// </summary>
    public interface IUnitOfWork : IDapperUnitOfWork
    {
        IUserRepository UserRepository { get; }

        IRoleRepository RoleRepository { get; }

        IScreenRepository ScreenRepository { get; }

        ITenantRepository TenantRepository { get; }

        IRefreshTokenRepository RefreshTokenRepository { get; }

        IAuditLogRepository AuditLogRepository { get; }

        /// <summary>Veículos.</summary>
        IVehicleRepository VehicleRepository { get; }

        /// <summary>Gastos do veículo.</summary>
        IVehicleExpenseRepository VehicleExpenseRepository { get; }

        /// <summary>Tipos de gasto, mantidos pela revenda.</summary>
        IExpenseTypeRepository ExpenseTypeRepository { get; }

        /// <summary>Fotos do veículo.</summary>
        IVehiclePhotoRepository VehiclePhotoRepository { get; }

        /// <summary>Documentos do veículo.</summary>
        IVehicleDocumentRepository VehicleDocumentRepository { get; }

        /// <summary>Histórico de status do veículo.</summary>
        IVehicleStatusHistoryRepository VehicleStatusHistoryRepository { get; }
    }
}
