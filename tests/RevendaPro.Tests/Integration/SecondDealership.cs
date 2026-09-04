using Foundation.Domain.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using RevendaPro.Domain.Entities;
using RevendaPro.Domain.Enums;
using RevendaPro.Domain.Interfaces;
using RevendaPro.Domain.Interfaces.Security;

namespace RevendaPro.Tests.Integration
{
    /// <summary>
    /// A outra revenda, com carro, gasto, foto, documento e proposta próprios.
    ///
    /// Montada pelas <b>mesmas entidades e repositórios que a API usa</b>, e jamais por
    /// <c>INSERT</c> na mão: uma linha inserida à mão pode ser uma linha que o sistema não sabe
    /// criar, e aí o teste passaria a provar a isolação de um dado irreal.
    ///
    /// Ela existe para uma pergunta só: <b>a revenda A alcança alguma coisa daqui?</b> A
    /// resposta certa é 404 em tudo — para quem está na empresa A, o registro da B simplesmente
    /// não existe (RNF-04).
    /// </summary>
    public sealed record SecondDealership(
        int IdTenant,
        string AdminEmail,
        Guid AdminCode,
        Guid RoleCode,
        Guid VehicleCode,
        string Plate,
        Guid ExpenseCode,
        Guid PhotoCode,
        Guid DocumentCode,
        Guid ProposalCode)
    {
        /// <summary>Constrói a segunda revenda dentro da API que está no ar.</summary>
        /// <param name="api">A API no ar.</param>
        /// <param name="password">Senha do administrador dela.</param>
        /// <returns>O que os testes precisam alcançar — ou deixar de alcançar.</returns>
        public static async Task<SecondDealership> BuildAsync(ApiFixture api, string password)
        {
            ArgumentNullException.ThrowIfNull(api);

            using var scope = api.Scope();

            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

            // Toda escrita é relida pelo código depois do commit.
            //
            // A escrita é bufferizada e o Id vem do banco, então o objeto em memória continua
            // com Id zero — e o zero vira violação de chave estrangeira na linha seguinte. O
            // próprio DbInitializer faz esta releitura, pelo mesmo motivo.
            var tenant = Tenant.Create("Revenda da Esquina");
            unitOfWork.TenantRepository.Add(tenant);
            await unitOfWork.CommitAsync().ConfigureAwait(false);

            tenant = await unitOfWork.TenantRepository.GetByCodeAsync(tenant.Code).ConfigureAwait(false)
                ?? throw new InvalidOperationException("A segunda revenda não foi criada.");

            // O perfil recebe todas as telas: o objetivo é provar que nem assim ele alcança a
            // outra empresa. Isolação que dependesse de permissão curta não seria isolação.
            var role = Role.Create(tenant.Id, "Administrador", "Acesso integral.", isSystem: true);
            unitOfWork.RoleRepository.Add(role);
            await unitOfWork.CommitAsync().ConfigureAwait(false);

            role = await unitOfWork.RoleRepository.GetByCodeAsync(role.Code).ConfigureAwait(false)
                ?? throw new InvalidOperationException("O perfil da segunda revenda não foi criado.");

            var screens = await unitOfWork.ScreenRepository.GetAllAsync().ConfigureAwait(false);
            unitOfWork.RoleRepository.ReplaceScreens(
                role.Id, [.. screens.Select(screen => screen.Id)], Entity.SystemActor);

            const string email = "dona@esquina.local";

            var admin = User.Create(tenant.Id, "Dona da Esquina", email, hasher.Hash(password));
            unitOfWork.UserRepository.Add(admin);
            await unitOfWork.CommitAsync().ConfigureAwait(false);

            admin = await unitOfWork.UserRepository.GetByCodeAsync(admin.Code).ConfigureAwait(false)
                ?? throw new InvalidOperationException("A dona da segunda revenda não foi criada.");

            unitOfWork.UserRepository.ReplaceRoles(admin.Id, [role.Id], Entity.SystemActor);
            await unitOfWork.CommitAsync().ConfigureAwait(false);

            var vehicle = Vehicle.Create(
                tenant.Id, "ESQ4E56", "9BWZZZ377VT111222", "Volkswagen", "Gol", 2019, 2018);

            vehicle.SetPurchase(31_000m, new DateOnly(2026, 8, 12), "Leilão da Esquina", PaymentMethod.Cash);
            vehicle.SetPricing(45_000m, 42_000m, null, null);

            unitOfWork.VehicleRepository.Add(vehicle);
            await unitOfWork.CommitAsync().ConfigureAwait(false);

            vehicle = await unitOfWork.VehicleRepository
                .GetByCodeAsync(tenant.Id, vehicle.Code)
                .ConfigureAwait(false)
                ?? throw new InvalidOperationException("O carro da segunda revenda não foi criado.");

            var types = await unitOfWork.ExpenseTypeRepository
                .ListByTenantAsync(tenant.Id)
                .ConfigureAwait(false);

            var expenseType = types.FirstOrDefault();

            if (expenseType is null)
            {
                var created = ExpenseType.Create(tenant.Id, "Peças", null, 0);
                unitOfWork.ExpenseTypeRepository.Add(created);
                await unitOfWork.CommitAsync().ConfigureAwait(false);

                expenseType = await unitOfWork.ExpenseTypeRepository
                    .GetByCodeAsync(tenant.Id, created.Code)
                    .ConfigureAwait(false)
                    ?? throw new InvalidOperationException("O tipo de gasto não foi criado.");
            }

            var expense = VehicleExpense.Create(
                vehicle.Id, "Pastilha de freio", expenseType.Id, 320m, new DateOnly(2026, 8, 20));

            unitOfWork.VehicleExpenseRepository.Add(expense);

            // Foto e documento são o caso que mais interessa: eles não carregam empresa, e
            // pendem do veículo. A isolação deles depende do join, e é exatamente onde um
            // WHERE esquecido não aparece em teste de unidade.
            var photo = VehiclePhoto.Create(
                vehicle.Id, VehiclePhotoKind.Finished,
                $"esquina/{Guid.NewGuid()}", "image/webp", 1024, 800, 600, 0);

            unitOfWork.VehiclePhotoRepository.Add(photo);

            var document = VehicleDocument.Create(
                vehicle.Id, VehicleDocumentKind.SaleInvoice,
                $"esquina/{Guid.NewGuid()}", "nota-da-esquina.pdf", "application/pdf", 2048);

            unitOfWork.VehicleDocumentRepository.Add(document);

            var proposal = Proposal.Create(
                vehicle.Id, "Comprador da Esquina", null, 44_000m, new DateOnly(2026, 9, 1),
                PaymentMethod.BankTransfer, SaleChannel.Direct, null, null, null);

            unitOfWork.ProposalRepository.Add(proposal);

            await unitOfWork.CommitAsync().ConfigureAwait(false);

            return new SecondDealership(
                tenant.Id,
                email,
                admin.Code,
                role.Code,
                vehicle.Code,
                vehicle.Plate,
                expense.Code,
                photo.Code,
                document.Code,
                proposal.Code);
        }
    }
}
