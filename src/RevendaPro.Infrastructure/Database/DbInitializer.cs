using System.Globalization;
using Foundation.Domain.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RevendaPro.Domain.Entities;
using RevendaPro.Domain.Enums;
using RevendaPro.Domain.Interfaces;
using RevendaPro.Domain.Interfaces.Security;
using RevendaPro.Infrastructure.Screens;
using RevendaPro.Infrastructure.Vehicles;
using RevendaPro.Shared.Helpers;
using RevendaPro.Shared.Settings;

namespace RevendaPro.Infrastructure.Database
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
            // O administrador recebe TODAS as telas do catálogo, e por isso jamais aparece
            // aqui. Ver GrantInitialScreensAsync.
            ["Gestor"] = ["dashboard", "vehicles", "sales", "market", "expense-types", "yards", "my-account"],
            ["Financeiro"] = ["dashboard", "vehicles", "sales", "market", "expense-types", "yards", "my-account"],
            ["Vendedor"] = ["dashboard", "vehicles", "sales", "my-account"],
            ["Oficina"] = ["dashboard", "vehicles", "my-account"]
        };

        /// <summary>Role descriptions. Portuguese: they are displayed to the user.</summary>
        private static readonly Dictionary<string, string> RoleDescriptions = new()
        {
            ["Administrador"] = "Acesso integral ao sistema.",
            ["Gestor"] = "Operação e relatórios.",
            ["Financeiro"] = "Custo dos veículos, vendas e relatórios financeiros.",
            ["Vendedor"] = "Estoque e vendas.",
            ["Oficina"] = "Reparo, gastos, fotos e documentos do veículo."
        };

        /// <summary>
        /// The demonstration crew: one person per role, plus six salespeople, because the
        /// list and the permission matrix only show their real shape with more than one row.
        ///
        /// Fictitious names, on a .local domain that resolves nowhere.
        /// </summary>
        private static readonly (string Name, string Email, string Role)[] DemoUsers =
        [
            ("Renata Albuquerque",        "renata.albuquerque@revendapro.local", "Gestor"),
            ("Sérgio Bittencourt",        "sergio.bittencourt@revendapro.local", "Financeiro"),
            ("Wagner Toledo",             "wagner.toledo@revendapro.local",      "Oficina"),
            ("João Vendedor",             "joao.vendedor@revendapro.local",      "Vendedor"),
            ("Michele Gonçalves Cardozo",   "michele.goncalves@revendapro.local", "Vendedor"),
            ("Camila Rezende",            "camila.rezende@revendapro.local",     "Vendedor"),
            ("Diego Fontoura",            "diego.fontoura@revendapro.local",     "Vendedor"),
            ("Priscila Amorim",           "priscila.amorim@revendapro.local",    "Vendedor"),
            ("Marcelo Assunção",          "marcelo.assuncao@revendapro.local",   "Vendedor")
        ];

        /// <summary>Runs the seeding.</summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task RunAsync(CancellationToken cancellationToken = default)
        {
            var tenant = await EnsureTenantAsync(cancellationToken).ConfigureAwait(false);

            await EnsureSystemRolesAsync(tenant, cancellationToken).ConfigureAwait(false);
            await EnsureExpenseTypesAsync(tenant, cancellationToken).ConfigureAwait(false);
            await EnsureAdministratorAsync(tenant, cancellationToken).ConfigureAwait(false);
            await EnsureDemoUsersAsync(tenant, cancellationToken).ConfigureAwait(false);
            await EnsureDemoYardAsync(tenant, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Cria o pátio de demonstração — quatro lugares e vinte carros —, quando ligado.
        ///
        /// Idempotente pela placa: rodar de novo jamais duplica um carro, e jamais desfaz o que
        /// alguém mexeu à mão num deles.
        ///
        /// <b>O carro de demonstração excluído volta na próxima subida</b>, e isso é consequência
        /// de usar a mesma conferência de placa que o cadastro usa — ela filtra por
        /// <c>IsActive = 1</c>, então a placa de uma linha excluída lê como livre. Vale como
        /// recurso: apagar carros e reiniciar a pilha devolve o pátio de demonstração inteiro.
        /// Quem quer o pátio limpo desliga <c>RevendaPro:SeedDemoVehicles</c> antes de subir.
        ///
        /// Ver <see cref="DemoYard"/> para o porquê de cada carro da lista.
        /// </summary>
        private async Task EnsureDemoYardAsync(Tenant tenant, CancellationToken cancellationToken)
        {
            if (!_settings.SeedDemoVehicles)
            {
                return;
            }

            var places = await EnsureDemoPlacesAsync(tenant, cancellationToken).ConfigureAwait(false);

            var types = await unitOfWork.ExpenseTypeRepository
                .ListByTenantAsync(tenant.Id, cancellationToken)
                .ConfigureAwait(false);

            var typeIdsByName = types.ToDictionary(
                type => type.Name, type => type.Id, StringComparer.OrdinalIgnoreCase);

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var created = 0;

            foreach (var car in DemoYard.Cars)
            {
                var taken = await unitOfWork.VehicleRepository
                    .IdentifierExistsAsync(tenant.Id, car.Plate, car.Chassis, ignoreId: null, cancellationToken)
                    .ConfigureAwait(false);

                if (taken)
                {
                    continue;
                }

                await CreateDemoCarAsync(tenant, car, places, typeIdsByName, today, cancellationToken)
                    .ConfigureAwait(false);

                created++;
            }

            if (created > 0)
            {
                logger.LogInformation("{Count} demonstration vehicle(s) created.", created);
            }
        }

        /// <summary>Os quatro pátios da demonstração, pelo nome. Idempotente pelo nome.</summary>
        private async Task<Dictionary<string, int>> EnsureDemoPlacesAsync(
            Tenant tenant,
            CancellationToken cancellationToken)
        {
            var existing = await unitOfWork.YardRepository
                .ListByTenantAsync(tenant.Id, cancellationToken)
                .ConfigureAwait(false);

            var byName = existing.ToDictionary(
                yard => yard.Name, yard => yard.Id, StringComparer.OrdinalIgnoreCase);

            var missing = DemoYard.Places
                .Where(place => !byName.ContainsKey(place.Name))
                .ToList();

            if (missing.Count == 0)
            {
                return byName;
            }

            foreach (var place in missing)
            {
                unitOfWork.YardRepository.Add(
                    Yard.Create(tenant.Id, place.Name, place.Kind, place.Position));
            }

            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

            var saved = await unitOfWork.YardRepository
                .ListByTenantAsync(tenant.Id, cancellationToken)
                .ConfigureAwait(false);

            return saved.ToDictionary(
                yard => yard.Name, yard => yard.Id, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Um carro da demonstração, inteiro: compra, esteira, pátio, gastos e venda.
        ///
        /// A esteira é andada de verdade, um passo por vez, e cada passo grava o histórico —
        /// escrever o status final direto criaria um carro que a própria regra do domínio
        /// recusaria, e uma linha do tempo que começa no fim.
        /// </summary>
        private async Task CreateDemoCarAsync(
            Tenant tenant,
            DemoCar car,
            IReadOnlyDictionary<string, int> places,
            IReadOnlyDictionary<string, int> expenseTypes,
            DateOnly today,
            CancellationToken cancellationToken)
        {
            var vehicle = Vehicle.Create(
                tenant.Id, car.Plate, car.Chassis, car.Brand, car.Model,
                car.ModelYear, (short)(car.ModelYear - 1));

            vehicle.SetDetails(
                car.Version, car.Color, car.Fuel, car.Transmission, renavam: null, notes: null);

            // A origem sai de quem vendeu, em vez de virar mais uma coluna do catálogo: leilão,
            // particular e loja é exatamente o que os nomes de fornecedor já dizem.
            var origin = car.Supplier.StartsWith("Leilão", StringComparison.OrdinalIgnoreCase)
                ? VehicleOrigin.Auction
                : car.Supplier.StartsWith("Particular", StringComparison.OrdinalIgnoreCase)
                    ? VehicleOrigin.Individual
                    : VehicleOrigin.Store;

            vehicle.SetOrigin(origin, hasDamage: false, damageDescription: null);
            vehicle.UpdateMileage(car.Mileage);

            vehicle.SetPurchase(
                car.PurchasePrice,
                today.AddDays(-car.BoughtDaysAgo),
                car.Supplier,
                PaymentMethod.BankTransfer);

            if (places.TryGetValue(car.Place, out var idYard))
            {
                vehicle.MoveToYard(idYard);
            }

            unitOfWork.VehicleRepository.Add(vehicle);
            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

            // O Id só existe depois do commit, e os gastos e a venda apontam para ele.
            var saved = await unitOfWork.VehicleRepository
                .GetByCodeAsync(tenant.Id, vehicle.Code, cancellationToken)
                .ConfigureAwait(false);

            if (saved is null)
            {
                return;
            }

            WalkThePipeline(saved, car);

            foreach (var expense in car.Expenses)
            {
                if (!expenseTypes.TryGetValue(expense.Type, out var idType))
                {
                    continue;
                }

                unitOfWork.VehicleExpenseRepository.Add(VehicleExpense.Create(
                    saved.Id, expense.Description, idType, expense.Amount,
                    today.AddDays(-expense.DaysAgo)));
            }

            if (car.Sale is { } sale)
            {
                var byPartner = sale.PartnerCutPercent is not null;

                unitOfWork.SaleRepository.Add(Sale.Create(
                    saved.Id,
                    idProposal: null,
                    today.AddDays(-sale.DaysAgo),
                    sale.Amount,
                    PaymentMethod.BankTransfer,
                    byPartner ? SaleChannel.PartnerStore : SaleChannel.Direct,
                    byPartner ? car.Place : null,
                    sale.PartnerCutPercent,
                    partnerCutAmount: null,
                    sale.Commission,
                    commissionNotes: null,
                    sale.Buyer,
                    buyerDocument: null,
                    buyerPhone: null,
                    tradeInValue: null,
                    notes: null));
            }

            unitOfWork.VehicleRepository.Update(saved);

            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Anda a esteira até onde o carro parou, gravando cada passo.
        ///
        /// A ordem sai da própria regra do domínio: um carro em análise vai para comprado, e de
        /// lá para reparo ou para pronto. Vendido tem porta única, e por isso ele sai por
        /// <c>Sell</c>, e jamais por uma mudança de status comum.
        /// </summary>
        private void WalkThePipeline(Vehicle vehicle, DemoCar car)
        {
            var steps = new List<VehicleStatus> { VehicleStatus.Purchased };

            if (car.Status == VehicleStatus.InRepair)
            {
                steps.Add(VehicleStatus.InRepair);
            }
            else if (car.Status != VehicleStatus.Purchased)
            {
                steps.Add(VehicleStatus.ReadyForSale);

                if (car.Status is VehicleStatus.Advertised or VehicleStatus.Negotiating)
                {
                    steps.Add(VehicleStatus.Advertised);
                }

                if (car.Status == VehicleStatus.Negotiating)
                {
                    steps.Add(VehicleStatus.Negotiating);
                }
            }

            foreach (var step in steps)
            {
                var from = vehicle.ChangeStatus(step);

                unitOfWork.VehicleStatusHistoryRepository.Add(
                    VehicleStatusHistory.Create(vehicle.Id, from, step));
            }

            if (car.Status != VehicleStatus.Sold)
            {
                return;
            }

            var previous = vehicle.Sell();

            unitOfWork.VehicleStatusHistoryRepository.Add(
                VehicleStatusHistory.Create(vehicle.Id, previous, VehicleStatus.Sold));
        }


        /// <summary>
        /// Creates the demonstration users, when they are turned on. Idempotent by e-mail:
        /// running it again neither duplicates a person nor resets a password changed by hand.
        /// </summary>
        private async Task EnsureDemoUsersAsync(Tenant tenant, CancellationToken cancellationToken)
        {
            if (!_settings.SeedDemoUsers)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(_settings.DemoPassword))
            {
                throw new InvalidOperationException(
                    "RevendaPro:SeedDemoUsers is on but RevendaPro:DemoPassword is empty. " +
                    "Set it, or turn the seeding off.");
            }

            var roles = await unitOfWork.RoleRepository
                .ListByTenantAsync(tenant.Id, cancellationToken)
                .ConfigureAwait(false);

            var roleIdsByName = roles.ToDictionary(r => r.Name, r => r.Id, StringComparer.OrdinalIgnoreCase);

            var random = new Random();

            var pending = new List<(string Email, string Role)>();

            foreach (var (name, email, role) in DemoUsers)
            {
                var address = email.Trim().ToLowerInvariant();

                var exists = await unitOfWork.UserRepository
                    .EmailExistsAsync(tenant.Id, address, ignoreId: null, cancellationToken)
                    .ConfigureAwait(false);

                if (exists || !roleIdsByName.ContainsKey(role))
                {
                    continue;
                }

                var person = User.Create(
                    tenant.Id, name, address, passwordHasher.Hash(_settings.DemoPassword));

                // The document is required on the screen, so a demonstration row that lacks
                // one cannot even be saved again from the form.
                person.Update(name, address, RandomCpf(random), phone: null);

                unitOfWork.UserRepository.Add(person);

                pending.Add((address, role));
            }

            if (pending.Count == 0)
            {
                return;
            }

            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

            // The role can only be attached after the commit: the user has no Id before it.
            foreach (var (address, role) in pending)
            {
                var saved = await unitOfWork.UserRepository
                    .GetByEmailAsync(address, cancellationToken)
                    .ConfigureAwait(false);

                if (saved is not null)
                {
                    unitOfWork.UserRepository.ReplaceRoles(
                        saved.Id, [roleIdsByName[role]], Entity.SystemActor);
                }
            }

            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

            logger.LogInformation("{Count} demonstration user(s) created.", pending.Count);
        }

        /// <summary>
        /// A random, valid CPF for a demonstration row.
        ///
        /// The check digits are found by trying the hundred possibilities against the very
        /// validator the application uses, instead of reimplementing the arithmetic here. A
        /// hundred iterations cost nothing, and the number that comes out is valid by
        /// construction — there is no second copy of the rule to drift from the first.
        /// </summary>
        private static string RandomCpf(Random random)
        {
            var body = string.Concat(Enumerable.Range(0, 9).Select(_ => random.Next(10)));

            for (var candidate = 0; candidate < 100; candidate++)
            {
                var cpf = body + candidate.ToString("D2", CultureInfo.InvariantCulture);

                if (BrazilianDocuments.IsValidCpf(cpf))
                {
                    return cpf;
                }
            }

            // Unreachable: every nine digit body has exactly one valid pair of check digits.
            throw new InvalidOperationException($"No valid CPF for the body {body}.");
        }

        /// <summary>
        /// Gives a new tenant the initial types of expense (RF-09).
        ///
        /// Nobody registers a dozen types before entering the first expense. From here on the
        /// list belongs to the dealership: it edits, adds and reorders as its own work demands.
        ///
        /// Idempotent by name: running it again neither duplicates a type nor overwrites the
        /// keywords somebody adjusted by hand.
        /// </summary>
        private async Task EnsureExpenseTypesAsync(Tenant tenant, CancellationToken cancellationToken)
        {
            var existing = await unitOfWork.ExpenseTypeRepository
                .ListByTenantAsync(tenant.Id, cancellationToken)
                .ConfigureAwait(false);

            var existingNames = existing
                .Select(type => type.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var created = 0;

            for (var position = 0; position < ExpenseTypeCatalog.Initial.Length; position++)
            {
                var (name, keywords) = ExpenseTypeCatalog.Initial[position];

                if (existingNames.Contains(name))
                {
                    continue;
                }

                unitOfWork.ExpenseTypeRepository.Add(
                    ExpenseType.Create(tenant.Id, name, keywords, position));

                created++;
            }

            if (created == 0)
            {
                return;
            }

            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

            logger.LogInformation("{Count} expense type(s) created.", created);
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

            // A lista de perfis vem das descrições, e não das telas: o administrador recebe
            // todas as telas e por isso fica fora do mapa de telas iniciais.
            var missing = RoleDescriptions.Keys.Where(name => !existingNames.Contains(name)).ToList();

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
                // The administrator gets every screen there is, derived from the catalogue and
                // never from a list written by hand.
                //
                // A hand written list drifts: a screen added to the catalogue would reach the
                // administrator of an existing database, through the synchronizer, and stay
                // out of a database created from scratch — the same role, two different sets
                // of permissions, depending on when the company was created.
                var ids = role.Name == ScreenCatalog.AdministratorRole
                    ? [.. screensByKey.Values]
                    : InitialScreens[role.Name]
                        .Where(screensByKey.ContainsKey)
                        .Select(key => screensByKey[key])
                        .ToList();

                unitOfWork.RoleRepository.ReplaceScreens(role.Id, ids, Entity.SystemActor);
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

            user.Update("Administrador", email, RandomCpf(new Random()), phone: null);

            unitOfWork.UserRepository.Add(user);
            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

            var saved = await unitOfWork.UserRepository
                .GetByEmailAsync(email, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new InvalidOperationException("Administrator user could not be created.");

            unitOfWork.UserRepository.ReplaceRoles(saved.Id, [administrator.Id], Entity.SystemActor);
            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

            logger.LogInformation("Administrator user \"{Email}\" created.", email);
        }
    }
}
