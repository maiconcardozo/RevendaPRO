using Foundation.Dapper;
using Foundation.Domain.Interfaces.Data;
using Foundation.Domain.Interfaces.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RevendaPro.Domain.Interfaces;
using RevendaPro.Domain.Interfaces.Reference;
using RevendaPro.Domain.Interfaces.Repositories;
using RevendaPro.Domain.Interfaces.Security;
using RevendaPro.Domain.Interfaces.Storage;
using RevendaPro.Infrastructure.Database;
using RevendaPro.Infrastructure.Database.Contexts;
using RevendaPro.Infrastructure.Data.MariaDb;
using RevendaPro.Infrastructure.Reference;
using RevendaPro.Infrastructure.Repositories.Common;
using RevendaPro.Infrastructure.Repositories.Roles;
using RevendaPro.Infrastructure.Repositories.Screens;
using RevendaPro.Infrastructure.Repositories.Users;
using RevendaPro.Infrastructure.Repositories.Sales;
using RevendaPro.Infrastructure.Repositories.Vehicles;
using RevendaPro.Infrastructure.Screens;
using RevendaPro.Infrastructure.Security;
using RevendaPro.Infrastructure.Storage;
using RevendaPro.Shared.Settings;

namespace RevendaPro.Infrastructure.Configuration
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
            services.Configure<StorageSettings>(configuration.GetSection(StorageSettings.SectionName));
            services.Configure<FipeSettings>(configuration.GetSection(FipeSettings.SectionName));

            var connectionString = configuration.GetSection(RevendaProSettings.SectionName)["ConnectionString"]
                ?? throw new InvalidOperationException("RevendaPro:ConnectionString is not configured.");

            // Registered ONLY so "dotnet ef" and the startup migration can run. No repository
            // and no handler resolves it: at runtime the access path is Dapper. See ADR-0003.
            services.AddDbContext<RevendaProDbContext>(options => options.UseMySQL(connectionString));

            // Foundation: Dapper unit of work, generic repository and the type handlers (Guid,
            // DateOnly and their nullable forms).
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
            services.AddSingleton<IUserPhotoStorage, BucketUserPhotoStorage>();

            // File storage through the S3 API. Which provider answers is configuration, and
            // never a dependency: MinIO locally, Cloudflare R2 in production. See ADR-0004.
            services.AddSingleton<IFileStorage, S3FileStorage>();
            services.AddSingleton<IImageProcessor, SkiaImageProcessor>();
            services.AddSingleton<StorageInitializer>();

            // The reference table, over HTTP. Which mirror answers is configuration, and the
            // timeout is short on purpose: a reference table is never allowed to hold up an
            // operation. See ADR-0005.
            var fipe = configuration.GetSection(FipeSettings.SectionName).Get<FipeSettings>()
                ?? new FipeSettings();

            services.AddHttpClient<IFipeCatalog, FipeHttpCatalog>(client =>
            {
                client.BaseAddress = new Uri(fipe.BaseUrl.TrimEnd('/') + "/");
                client.Timeout = TimeSpan.FromSeconds(fipe.TimeoutInSeconds);
            });

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

            services.AddScoped<Func<IDapperUnitOfWork, IVehicleRepository>>(
                _ => uow => new VehicleRepository(uow));

            services.AddScoped<Func<IDapperUnitOfWork, IVehicleExpenseRepository>>(
                _ => uow => new VehicleExpenseRepository(uow));

            services.AddScoped<Func<IDapperUnitOfWork, IExpenseTypeRepository>>(
                _ => uow => new ExpenseTypeRepository(uow));

            services.AddScoped<Func<IDapperUnitOfWork, IVehicleStatusHistoryRepository>>(
                _ => uow => new VehicleStatusHistoryRepository(uow));

            services.AddScoped<Func<IDapperUnitOfWork, IVehiclePhotoRepository>>(
                _ => uow => new VehiclePhotoRepository(uow));

            services.AddScoped<Func<IDapperUnitOfWork, IVehicleDocumentRepository>>(
                _ => uow => new VehicleDocumentRepository(uow));

            services.AddScoped<Func<IDapperUnitOfWork, IProposalRepository>>(
                _ => uow => new ProposalRepository(uow));

            services.AddScoped<Func<IDapperUnitOfWork, ISaleRepository>>(
                _ => uow => new SaleRepository(uow));
        }
    }
}
