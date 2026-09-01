using Foundation.Dapper;
using Foundation.Domain.Interfaces.Data;
using Foundation.Domain.Interfaces.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RevendaPro.Global.Domain.Interfaces;
using RevendaPro.Global.Domain.Interfaces.Repositories;
using RevendaPro.Global.Domain.Interfaces.Security;
using RevendaPro.Global.Infrastructure.Database;
using RevendaPro.Global.Infrastructure.Database.Contexts;
using RevendaPro.Global.Infrastructure.Data.MariaDb;
using RevendaPro.Global.Infrastructure.Repositories.Common;
using RevendaPro.Global.Infrastructure.Repositories.Roles;
using RevendaPro.Global.Infrastructure.Repositories.Screens;
using RevendaPro.Global.Infrastructure.Repositories.Users;
using RevendaPro.Global.Infrastructure.Screens;
using RevendaPro.Global.Infrastructure.Security;
using RevendaPro.Global.Infrastructure.Services.Storage;
using RevendaPro.Global.Shared.Settings;

namespace RevendaPro.Global.Infrastructure.Configuration
{
    /// <summary>Registers the Infrastructure layer.</summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers persistence, security and storage.
        /// </summary>
        /// <param name="services">The service collection to add services to.</param>
        /// <param name="configuration">Application configuration.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddInfrastructure(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configuration);

            services.Configure<RevendaProSettings>(configuration.GetSection(RevendaProSettings.SectionName));
            services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));

            var connectionString = configuration.GetSection(RevendaProSettings.SectionName)["ConnectionString"]
                ?? throw new InvalidOperationException("RevendaPro:ConnectionString is not configured.");

            // Registered ONLY so "dotnet ef" and the startup migration can run. No repository
            // and no handler resolves it: at runtime the access path is Dapper. See ADR-0003.
            services.AddDbContext<RevendaProDbContext>(options => options.UseMySQL(connectionString));

            // Foundation: Dapper unit of work, generic repository and the Guid type handler.
            services.AddDapperServices();

            services.AddMemoryCache();

            services.AddScoped<ISqlConnectionFactory, MySqlConnectionFactory>();

            AddRepositoryFactories(services);

            // Registered against both contracts as the SAME instance: the write buffer and
            // the transaction have to be shared, or Commit would flush an empty queue.
            services.AddScoped<UnitOfWork.RevendaProUnitOfWork>();
            services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<UnitOfWork.RevendaProUnitOfWork>());
            services.AddScoped<IDapperUnitOfWork>(sp => sp.GetRequiredService<UnitOfWork.RevendaProUnitOfWork>());

            services.AddSingleton<IPasswordHasher, PasswordHasherService>();
            services.AddScoped<ITokenService, JwtTokenService>();
            services.AddScoped<IPermissionService, PermissionService>();
            services.AddSingleton<IPhotoStorageService, DiskPhotoStorageService>();

            services.AddScoped<SchemaMigrator>();
            services.AddScoped<ScreenSynchronizer>();
            services.AddScoped<DbInitializer>();

            return services;
        }

        /// <summary>
        /// Repositories are resolved as factories over the unit of work, so every one of them
        /// shares the same connection, transaction and write buffer. Same shape as
        /// AuthUnitOfWork in source/Global/Authentication.
        /// </summary>
        private static void AddRepositoryFactories(IServiceCollection services)
        {
            services.AddScoped<Func<IDapperUnitOfWork, IUserRepository>>(
                _ => uow => new UserRepository(uow));

            services.AddScoped<Func<IDapperUnitOfWork, IRoleRepository>>(
                _ => uow => new RoleRepository(uow));

            services.AddScoped<Func<IDapperUnitOfWork, IScreenRepository>>(
                _ => uow => new ScreenRepository(uow));

            services.AddScoped<Func<IDapperUnitOfWork, ITenantRepository>>(
                _ => uow => new TenantRepository(uow));

            services.AddScoped<Func<IDapperUnitOfWork, IRefreshTokenRepository>>(
                _ => uow => new RefreshTokenRepository(uow));

            services.AddScoped<Func<IDapperUnitOfWork, IAuditLogRepository>>(
                _ => uow => new AuditLogRepository(uow));
        }
    }
}
