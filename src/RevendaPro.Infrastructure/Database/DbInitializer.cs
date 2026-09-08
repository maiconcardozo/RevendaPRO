using System.Globalization;
using Foundation.Domain.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RevendaPro.Domain.Entities;
using RevendaPro.Domain.Enums;
using RevendaPro.Domain.Interfaces;
using RevendaPro.Domain.Interfaces.Security;
using RevendaPro.Infrastructure.Screens;
using RevendaPro.Infrastructure.Suppliers;
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
            ["Gestor"] = ["dashboard", "vehicles", "customers", "sales", "market", "expense-types", "yards", "suppliers", "my-account"],
            ["Financeiro"] = ["dashboard", "vehicles", "customers", "sales", "market", "expense-types", "yards", "suppliers", "my-account"],
            ["Vendedor"] = ["dashboard", "vehicles", "customers", "sales", "my-account"],
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
            await EnsureSupplierSegmentsAsync(tenant, cancellationToken).ConfigureAwait(false);
            await EnsureAdministratorAsync(tenant, cancellationToken).ConfigureAwait(false);
            await EnsureDemoUsersAsync(tenant, cancellationToken).ConfigureAwait(false);
            await EnsureDemoYardAsync(tenant, cancellationToken).ConfigureAwait(false);
            await EnsureDemoCustomersAsync(tenant, cancellationToken).ConfigureAwait(false);
            await EnsureCustomersAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// O aproveitamento dos clientes (M21): toda proposta e toda venda que ainda aponta para
        /// cliente nenhum ganha um, em cada revenda, sem ninguém redigitar nada.
        ///
        /// Casa por <b>documento</b> quando há, depois por <b>telefone</b>, e por último por
        /// <b>nome igual</b>; quem cai em nenhum caso vira um cliente próprio. Da mais antiga para
        /// a mais nova, para o cliente nascer de quem apareceu primeiro. Idempotente: percorre só
        /// quem está sem <c>IdCustomer</c>, e depois da primeira subida percorre nada.
        /// </summary>
        private async Task EnsureCustomersAsync(CancellationToken cancellationToken)
        {
            var tenants = await unitOfWork.TenantRepository
                .ListAllAsync(cancellationToken)
                .ConfigureAwait(false);

            foreach (var tenant in tenants)
            {
                var sales = await unitOfWork.SaleRepository
                    .ListWithoutCustomerAsync(tenant.Id, cancellationToken)
                    .ConfigureAwait(false);

                var proposals = await unitOfWork.ProposalRepository
                    .ListWithoutCustomerAsync(tenant.Id, cancellationToken)
                    .ConfigureAwait(false);

                if (sales.Count == 0 && proposals.Count == 0)
                {
                    continue;
                }

                // A venda vai primeiro: é quem tem o documento, e o documento é o casamento mais
                // seguro. A proposta do mesmo cliente encontra a venda pelo telefone.
                foreach (var sale in sales)
                {
                    var customer = await ResolveCustomerAsync(
                        tenant.Id, sale.BuyerName, sale.BuyerDocument, sale.BuyerPhone, cancellationToken)
                        .ConfigureAwait(false);

                    sale.AssignCustomer(customer.Id);
                    unitOfWork.SaleRepository.Update(sale);
                }

                foreach (var proposal in proposals)
                {
                    var customer = await ResolveCustomerAsync(
                        tenant.Id, proposal.ProspectName, document: null, proposal.ProspectPhone, cancellationToken)
                        .ConfigureAwait(false);

                    proposal.AssignCustomer(customer.Id);
                    unitOfWork.ProposalRepository.Update(proposal);
                }

                await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

                logger.LogInformation(
                    "Tenant {Tenant}: {Sales} sale(s) and {Proposals} proposal(s) linked to customers.",
                    tenant.Name, sales.Count, proposals.Count);
            }
        }

        /// <summary>
        /// O cliente que uma venda ou proposta antiga descreve: o que já existe com esse documento,
        /// ou com esse telefone, ou com esse nome — completando o que ele tinha em branco —, ou um
        /// novo. Grava na hora, porque a próxima linha pode ser da mesma pessoa.
        /// </summary>
        private async Task<Customer> ResolveCustomerAsync(
            int idTenant,
            string name,
            string? document,
            string? phone,
            CancellationToken cancellationToken)
        {
            var found = await FindCustomerAsync(idTenant, name, document, phone, cancellationToken)
                .ConfigureAwait(false);

            if (found is not null)
            {
                found.FillBlanks(document, phone);
                unitOfWork.CustomerRepository.Update(found);
                await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

                return found;
            }

            var customer = Customer.Create(idTenant, name, phone);
            customer.FillBlanks(document, phone: null);

            unitOfWork.CustomerRepository.Add(customer);
            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

            // O Id nasce no banco: a leitura de volta é o que o traz.
            return await FindCustomerAsync(idTenant, name, customer.Document, customer.Phone, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Customer '{name}' was written and could not be read back.");
        }

        private async Task<Customer?> FindCustomerAsync(
            int idTenant,
            string name,
            string? document,
            string? phone,
            CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(document))
            {
                var byDocument = await unitOfWork.CustomerRepository
                    .FindByDocumentAsync(idTenant, document, ignoreId: null, cancellationToken)
                    .ConfigureAwait(false);

                if (byDocument is not null)
                {
                    return byDocument;
                }
            }

            if (!string.IsNullOrWhiteSpace(phone))
            {
                var byPhone = await unitOfWork.CustomerRepository
                    .FindByPhoneAsync(idTenant, phone, cancellationToken)
                    .ConfigureAwait(false);

                if (byPhone.Count > 0)
                {
                    return byPhone[0];
                }
            }

            var byName = await unitOfWork.CustomerRepository
                .FindByNameAsync(idTenant, name, cancellationToken)
                .ConfigureAwait(false);

            // Nome igual só casa quando um dos lados está sem telefone: dois Joões com telefones
            // diferentes são duas pessoas.
            return byName.FirstOrDefault(customer =>
                customer.Phone is null || phone is null || customer.Phone == phone);
        }

        /// <summary>
        /// Os clientes da demonstração (M21), pelo telefone. Roda antes do aproveitamento, para
        /// as vendas e propostas de demonstração casarem com eles; e completa telefone e CPF de
        /// quem nasceu de uma venda antiga só com o nome.
        /// </summary>
        private async Task EnsureDemoCustomersAsync(Tenant tenant, CancellationToken cancellationToken)
        {
            if (!_settings.SeedDemoVehicles)
            {
                return;
            }

            var created = 0;

            foreach (var demo in DemoYard.Customers)
            {
                var byPhone = await unitOfWork.CustomerRepository
                    .FindByPhoneAsync(tenant.Id, demo.Phone, cancellationToken)
                    .ConfigureAwait(false);

                if (byPhone.Count > 0)
                {
                    continue;
                }

                var byName = await unitOfWork.CustomerRepository
                    .FindByNameAsync(tenant.Id, demo.Name, cancellationToken)
                    .ConfigureAwait(false);

                var existing = byName.FirstOrDefault(customer => customer.Phone is null);

                if (existing is not null)
                {
                    existing.FillBlanks(demo.Document, demo.Phone);
                    unitOfWork.CustomerRepository.Update(existing);
                    continue;
                }

                var customer = Customer.Create(tenant.Id, demo.Name, demo.Phone);
                customer.FillBlanks(demo.Document, phone: null);
                unitOfWork.CustomerRepository.Add(customer);
                created++;
            }

            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

            if (created > 0)
            {
                logger.LogInformation("{Count} demonstration customer(s) created.", created);
            }
        }

        private static DemoCustomer? DemoCustomerOf(string name) =>
            DemoYard.Customers.FirstOrDefault(customer =>
                string.Equals(customer.Name, name, StringComparison.OrdinalIgnoreCase));

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
            var suppliers = await EnsureDemoSuppliersAsync(tenant, cancellationToken).ConfigureAwait(false);

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
                    // O carro já está lá, de uma subida anterior: os gastos dele podem ter
                    // nascido antes da tabela de fornecedor, e o catálogo pode ter crescido.
                    await RefreshDemoExpensesAsync(tenant, car, typeIdsByName, suppliers, today, cancellationToken)
                        .ConfigureAwait(false);

                    continue;
                }

                await CreateDemoCarAsync(tenant, car, places, typeIdsByName, suppliers, today, cancellationToken)
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
        /// Os nove fornecedores da demonstração, pelo nome. Idempotente pelo nome, e cada um
        /// aponta para um ramo do catálogo — que já existe, porque os ramos são semeados antes.
        /// </summary>
        private async Task<Dictionary<string, int>> EnsureDemoSuppliersAsync(
            Tenant tenant,
            CancellationToken cancellationToken)
        {
            var segments = await unitOfWork.SupplierSegmentRepository
                .ListByTenantAsync(tenant.Id, cancellationToken)
                .ConfigureAwait(false);

            var segmentIdsByName = segments.ToDictionary(
                segment => segment.Name, segment => segment.Id, StringComparer.OrdinalIgnoreCase);

            var existing = await unitOfWork.SupplierRepository
                .ListByTenantAsync(tenant.Id, cancellationToken)
                .ConfigureAwait(false);

            var byName = existing.ToDictionary(
                supplier => supplier.Name, supplier => supplier.Id, StringComparer.OrdinalIgnoreCase);

            var missing = DemoYard.Suppliers
                .Where(supplier => !byName.ContainsKey(supplier.Name))
                .Where(supplier => segmentIdsByName.ContainsKey(supplier.Segment))
                .ToList();

            if (missing.Count == 0)
            {
                return byName;
            }

            foreach (var supplier in missing)
            {
                unitOfWork.SupplierRepository.Add(
                    Supplier.Create(tenant.Id, supplier.Name, segmentIdsByName[supplier.Segment]));
            }

            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

            logger.LogInformation("{Count} demonstration supplier(s) created.", missing.Count);

            var saved = await unitOfWork.SupplierRepository
                .ListByTenantAsync(tenant.Id, cancellationToken)
                .ConfigureAwait(false);

            return saved.ToDictionary(
                supplier => supplier.Name, supplier => supplier.Id, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>O fornecedor de um gasto de demonstração, quando ele tem um e o cadastro existe.</summary>
        private static int? SupplierOf(DemoExpense expense, IReadOnlyDictionary<string, int> suppliers) =>
            expense.Supplier is { } name && suppliers.TryGetValue(name, out var id) ? id : null;

        /// <summary>
        /// Põe em dia os gastos de um carro de demonstração que já estava no banco.
        ///
        /// Os vinte carros são idempotentes pela placa, então quem já tinha o pátio de
        /// demonstração jamais receberia os fornecedores — e teria de apagar o banco para ver o
        /// painel funcionando. Duas coisas acontecem aqui, e só elas: o gasto <b>sem</b>
        /// fornecedor, casado pela descrição com o catálogo, ganha o seu; e o gasto do catálogo
        /// que o carro ainda não tem é lançado, para o painel do mês abrir com o bloco preenchido
        /// também num banco antigo. O que alguém mexeu à mão fica como está.
        /// </summary>
        private async Task RefreshDemoExpensesAsync(
            Tenant tenant,
            DemoCar car,
            IReadOnlyDictionary<string, int> expenseTypes,
            IReadOnlyDictionary<string, int> suppliers,
            DateOnly today,
            CancellationToken cancellationToken)
        {
            if (car.Expenses.Length == 0)
            {
                return;
            }

            var vehicles = await unitOfWork.VehicleRepository
                .ListAsync(tenant.Id, car.Plate, null, null, null, null, null, cancellationToken)
                .ConfigureAwait(false);

            var vehicle = vehicles.FirstOrDefault(v => v.Plate == car.Plate);

            if (vehicle is null)
            {
                return;
            }

            var expenses = await unitOfWork.VehicleExpenseRepository
                .ListByVehicleAsync(vehicle.Id, cancellationToken)
                .ConfigureAwait(false);

            var filled = 0;
            var added = 0;

            foreach (var expense in expenses.Where(expense => expense.IdSupplier is null))
            {
                var match = car.Expenses.FirstOrDefault(demo =>
                    string.Equals(demo.Description, expense.Description, StringComparison.OrdinalIgnoreCase));

                if (match is null || SupplierOf(match, suppliers) is not { } idSupplier)
                {
                    continue;
                }

                expense.AssignSupplier(idSupplier);
                unitOfWork.VehicleExpenseRepository.Update(expense);
                filled++;
            }

            var proposals = await unitOfWork.ProposalRepository
                .ListByVehicleAsync(vehicle.Id, cancellationToken)
                .ConfigureAwait(false);

            if (proposals.Count == 0)
            {
                AddDemoProposals(vehicle.Id, car, today);
            }

            var known = expenses
                .Select(expense => expense.Description)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var demo in car.Expenses.Where(demo => !known.Contains(demo.Description)))
            {
                if (!expenseTypes.TryGetValue(demo.Type, out var idType))
                {
                    continue;
                }

                unitOfWork.VehicleExpenseRepository.Add(VehicleExpense.Create(
                    vehicle.Id, demo.Description, idType, demo.Amount,
                    today.AddDays(-demo.DaysAgo), idSupplier: SupplierOf(demo, suppliers)));

                added++;
            }

            if (filled == 0 && added == 0)
            {
                return;
            }

            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

            logger.LogInformation(
                "{Plate}: {Filled} demonstration expense(s) pointed at a supplier, {Added} added.",
                car.Plate, filled, added);
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
            IReadOnlyDictionary<string, int> suppliers,
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
                    today.AddDays(-expense.DaysAgo),
                    idSupplier: SupplierOf(expense, suppliers)));
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
                    buyerDocument: DemoCustomerOf(sale.Buyer)?.Document,
                    buyerPhone: DemoCustomerOf(sale.Buyer)?.Phone,
                    tradeInValue: null,
                    notes: null));
            }

            AddDemoProposals(saved.Id, car, today);

            unitOfWork.VehicleRepository.Update(saved);

            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// As propostas de demonstração de um carro (M21), com nome e telefone do cliente: o
        /// aproveitamento as liga ao cliente pelo telefone, como faria com uma proposta de verdade.
        /// </summary>
        private void AddDemoProposals(int idVehicle, DemoCar car, DateOnly today)
        {
            foreach (var demo in car.Proposals ?? [])
            {
                var proposal = Proposal.Create(
                    idVehicle,
                    demo.Customer,
                    DemoCustomerOf(demo.Customer)?.Phone,
                    demo.Amount,
                    today.AddDays(-demo.DaysAgo),
                    PaymentMethod.BankTransfer,
                    SaleChannel.Direct,
                    partnerCutPercent: null,
                    partnerCutAmount: null,
                    notes: null);

                if (demo.Declined)
                {
                    proposal.Decline();
                }

                unitOfWork.ProposalRepository.Add(proposal);
            }
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

        /// <summary>
        /// A revenda nasce com os ramos de fornecedor prontos, para ninguém precisar cadastrar
        /// ramo antes de cadastrar o primeiro fornecedor.
        ///
        /// Idempotente por nome ativo, igual ao tipo de gasto: rodar de novo jamais duplica um
        /// ramo, e um ramo renomeado fica como a revenda o deixou. A consequência é a mesma do
        /// tipo de gasto — um ramo do catálogo que foi excluído volta na próxima subida.
        /// </summary>
        private async Task EnsureSupplierSegmentsAsync(Tenant tenant, CancellationToken cancellationToken)
        {
            var existing = await unitOfWork.SupplierSegmentRepository
                .ListByTenantAsync(tenant.Id, cancellationToken)
                .ConfigureAwait(false);

            var existingNames = existing
                .Select(segment => segment.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var created = 0;

            for (var position = 0; position < SupplierSegmentCatalog.Initial.Length; position++)
            {
                var name = SupplierSegmentCatalog.Initial[position];

                if (existingNames.Contains(name))
                {
                    continue;
                }

                unitOfWork.SupplierSegmentRepository.Add(SupplierSegment.Create(tenant.Id, name, position));

                created++;
            }

            if (created == 0)
            {
                return;
            }

            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

            logger.LogInformation("{Count} supplier segment(s) created.", created);
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
