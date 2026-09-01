using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RevendaPro.Global.Domain.Entities;
using RevendaPro.Global.Domain.Interfaces;
using RevendaPro.Global.Domain.Interfaces.Security;
using RevendaPro.Global.Infrastructure.Screens;
using RevendaPro.Global.Shared.Settings;

namespace RevendaPro.Global.Infrastructure.Database
{
    /// <summary>
    /// Seeds the pilot tenant, the system roles and the administrator user.
    ///
    /// Idempotent: running it twice neither duplicates rows nor overwrites permission
    /// adjustments made by hand.
    /// </summary>
    public class DbInitializer(
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        IOptions<RevendaProSettings> settings,
        ILogger<DbInitializer> logger)
    {
        private readonly RevendaProSettings _settings = settings.Value;

        /// <summary>
        /// Screens granted to each system role AT CREATION. After that the permission matrix
        /// is in charge: the seeder never reapplies this table over an existing role.
        /// </summary>
        private static readonly Dictionary<string, string[]> InitialScreens = new()
        {
            ["Administrador"] = ["dashboard", "vehicles", "costs", "sales", "users", "roles", "my-account"],
            ["Gestor"] = ["dashboard", "vehicles", "costs", "sales", "my-account"],
            ["Financeiro"] = ["dashboard", "costs", "sales", "my-account"],
            ["Vendedor"] = ["dashboard", "vehicles", "sales", "my-account"],
            ["Oficina"] = ["dashboard", "vehicles", "costs", "my-account"]
        };

        /// <summary>Role descriptions. Portuguese: they are displayed to the user.</summary>
        private static readonly Dictionary<string, string> RoleDescriptions = new()
        {
            ["Administrador"] = "Acesso integral ao sistema.",
            ["Gestor"] = "Operação e relatórios.",
            ["Financeiro"] = "Custos, vendas e relatórios financeiros.",
            ["Vendedor"] = "Estoque e vendas.",
            ["Oficina"] = "Orçamento, reparo, fotos e documentos técnicos."
        };

        /// <summary>Runs the seeding.</summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task RunAsync(CancellationToken cancellationToken = default)
        {
            var tenant = await EnsureTenantAsync(cancellationToken).ConfigureAwait(false);

            await EnsureSystemRolesAsync(tenant, cancellationToken).ConfigureAwait(false);
            await EnsureAdministratorAsync(tenant, cancellationToken).ConfigureAwait(false);
        }

        private async Task<Tenant> EnsureTenantAsync(CancellationToken cancellationToken)
        {
            var existing = await unitOfWork.TenantRepository
                .GetFirstAsync(cancellationToken)
                .ConfigureAwait(false);

            if (existing is not null)
            {
                return existing;
            }

            var tenant = Tenant.Create(_settings.PilotTenant);

            unitOfWork.TenantRepository.Add(tenant);
            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

            logger.LogInformation("Pilot tenant \"{Name}\" created.", _settings.PilotTenant);

            // Read back so the Id assigned by the database is known.
            return await unitOfWork.TenantRepository.GetFirstAsync(cancellationToken).ConfigureAwait(false)
                   ?? throw new InvalidOperationException("Tenant could not be created.");
        }

        private async Task EnsureSystemRolesAsync(Tenant tenant, CancellationToken cancellationToken)
        {
            var existing = await unitOfWork.RoleRepository
                .ListByTenantAsync(tenant.Id, cancellationToken)
                .ConfigureAwait(false);

            var existingNames = existing
                .Select(r => r.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var missing = InitialScreens.Keys.Where(name => !existingNames.Contains(name)).ToList();

            if (missing.Count == 0)
            {
                return;
            }

            foreach (var name in missing)
            {
                unitOfWork.RoleRepository.Add(Role.Create(
                    tenant.Id, name, RoleDescriptions.GetValueOrDefault(name), isSystem: true));

                logger.LogInformation("System role \"{Name}\" created.", name);
            }

            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

            // Granting comes after the commit: the roles only have an Id once saved.
            await GrantInitialScreensAsync(tenant, missing, cancellationToken).ConfigureAwait(false);
        }

        private async Task GrantInitialScreensAsync(
            Tenant tenant,
            IReadOnlyCollection<string> roleNames,
            CancellationToken cancellationToken)
        {
            var screens = await unitOfWork.ScreenRepository
                .GetAllAsync(cancellationToken)
                .ConfigureAwait(false);

            var screensByKey = screens.ToDictionary(s => s.Key, s => s.Id, StringComparer.OrdinalIgnoreCase);

            var roles = await unitOfWork.RoleRepository
                .ListByTenantAsync(tenant.Id, cancellationToken)
                .ConfigureAwait(false);

            foreach (var role in roles.Where(r => roleNames.Contains(r.Name)))
            {
                var ids = InitialScreens[role.Name]
                    .Where(screensByKey.ContainsKey)
                    .Select(key => screensByKey[key])
                    .ToList();

                unitOfWork.RoleRepository.ReplaceScreens(role.Id, ids, BaseEntity.SystemActor);
            }

            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        private async Task EnsureAdministratorAsync(Tenant tenant, CancellationToken cancellationToken)
        {
            var email = _settings.AdminEmail.Trim().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(email))
            {
                throw new InvalidOperationException("RevendaPro:AdminEmail is not configured.");
            }

            var exists = await unitOfWork.UserRepository
                .EmailExistsAsync(tenant.Id, email, ignoreId: null, cancellationToken)
                .ConfigureAwait(false);

            if (exists)
            {
                return;
            }

            var administrator = await unitOfWork.RoleRepository
                .GetByNameAsync(tenant.Id, ScreenCatalog.AdministratorRole, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new InvalidOperationException("Administrator role not found.");

            var user = User.Create(
                tenant.Id, "Administrador", email, passwordHasher.Hash(_settings.AdminPassword));

            unitOfWork.UserRepository.Add(user);
            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

            var saved = await unitOfWork.UserRepository
                .GetByEmailAsync(email, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new InvalidOperationException("Administrator user could not be created.");

            unitOfWork.UserRepository.ReplaceRoles(saved.Id, [administrator.Id], BaseEntity.SystemActor);
            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

            logger.LogInformation("Administrator user \"{Email}\" created.", email);
        }
    }
}
