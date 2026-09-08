using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RevendaPro.Domain.Interfaces;
using RevendaPro.Infrastructure.Database;

namespace RevendaPro.Tests.Integration
{
    /// <summary>
    /// O aproveitamento dos clientes (M21), contra o MariaDB de verdade.
    ///
    /// Duas propostas com o mesmo telefone e uma venda com o mesmo telefone e um CPF, gravadas
    /// sem cliente como o sistema fazia antes do M21; a rotina de subida roda de novo, e as três
    /// apontam para <b>um</b> cliente, que ganhou o CPF da venda. Rodar a rotina uma segunda vez
    /// muda nada: é o que "idempotente" quer dizer.
    /// </summary>
    [Collection(ApiCollection.Name)]
    public class CustomerBackfillTests(ApiFixture api) : IAsyncLifetime
    {
        private HttpClient mine = default!;
        private Guid vehicleCode;
        private Guid secondVehicleCode;
        private string phone = string.Empty;

        /// <inheritdoc/>
        public async Task InitializeAsync()
        {
            mine = await api.ClientOfAsync("admin@revendapro.local");
            phone = $"519{Random.Shared.Next(10_000_000, 99_999_999)}";

            vehicleCode = await CarAsync();
            secondVehicleCode = await CarAsync();

            await mine.PostAsJsonAsync(Url($"/api/vehicles/{vehicleCode}/proposals"), new
            {
                prospectName = "Marcos Silva",
                prospectPhone = phone,
                amount = 20_000,
                date = "2026-08-05",
                paymentMethod = 1,
                channel = 1,
            });

            await mine.PostAsJsonAsync(Url($"/api/vehicles/{secondVehicleCode}/proposals"), new
            {
                prospectName = "Marcos Silva",
                prospectPhone = phone,
                amount = 21_000,
                date = "2026-08-06",
                paymentMethod = 1,
                channel = 1,
            });

            // A esteira até "pronto para venda": vendido só se chega pela venda, e a venda só
            // aceita um carro pronto.
            foreach (var status in new[] { 2, 4 })
            {
                var moved = await mine.PatchAsJsonAsync(
                    Url($"/api/vehicles/{secondVehicleCode}/status"), new { status, reason = "teste" });
                moved.IsSuccessStatusCode.Should().BeTrue(await moved.Content.ReadAsStringAsync());
            }

            var sold = await mine.PostAsJsonAsync(Url($"/api/vehicles/{secondVehicleCode}/sale"), new
            {
                date = "2026-08-07",
                amount = 21_000,
                paymentMethod = 1,
                channel = 1,
                commission = 0,
                buyerName = "Marcos Silva",
                buyerDocument = "39053344705",
                buyerPhone = phone,
            });

            sold.IsSuccessStatusCode.Should().BeTrue(await sold.Content.ReadAsStringAsync());
        }

        /// <inheritdoc/>
        public async Task DisposeAsync()
        {
            foreach (var code in new[] { vehicleCode, secondVehicleCode }.Where(code => code != Guid.Empty))
            {
                await mine.DeleteAsync(Url($"/api/vehicles/{code}"));
            }

            using var scope = api.Scope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var tenant = (await unitOfWork.TenantRepository.GetFirstAsync())!;

            foreach (var customer in await unitOfWork.CustomerRepository.FindByPhoneAsync(tenant.Id, phone))
            {
                unitOfWork.CustomerRepository.Remove(customer, "tests");
            }

            await unitOfWork.CommitAsync();
        }

        [Fact]
        public async Task TheStartUpRoutine_LinksEveryOldRow_ToOneCustomer_AndRunningItAgainChangesNothing()
        {
            await RunTheInitializerAsync();

            using var scope = api.Scope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var tenant = (await unitOfWork.TenantRepository.GetFirstAsync())!;

            var customers = await unitOfWork.CustomerRepository.FindByPhoneAsync(tenant.Id, phone);
            customers.Should().HaveCount(1, "duas propostas e uma venda com o mesmo telefone são uma pessoa");

            var customer = customers[0];
            customer.Name.Should().Be("Marcos Silva");
            customer.Document.Should().Be("39053344705", "o CPF veio da venda");

            var summary = (await unitOfWork.CustomerRepository.SummarizeByTenantAsync(tenant.Id))
                .Single(row => row.IdCustomer == customer.Id);

            summary.ProposalCount.Should().Be(2);
            summary.SaleCount.Should().Be(1);
            summary.BoughtTotal.Should().Be(21_000m);
            summary.LastDate.Should().Be(new DateOnly(2026, 8, 7));

            (await unitOfWork.ProposalRepository.ListWithoutCustomerAsync(tenant.Id)).Should().BeEmpty();
            (await unitOfWork.SaleRepository.ListWithoutCustomerAsync(tenant.Id)).Should().BeEmpty();

            await RunTheInitializerAsync();

            (await unitOfWork.CustomerRepository.FindByPhoneAsync(tenant.Id, phone)).Should().HaveCount(1);
        }

        [Fact]
        public async Task TheHistory_ListsTheCarsOfTheCustomer_NewestFirst()
        {
            await RunTheInitializerAsync();

            using var scope = api.Scope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var tenant = (await unitOfWork.TenantRepository.GetFirstAsync())!;
            var customer = (await unitOfWork.CustomerRepository.FindByPhoneAsync(tenant.Id, phone))[0];

            var proposals = await unitOfWork.CustomerRepository.ListProposalsAsync(tenant.Id, customer.Id);
            var sales = await unitOfWork.CustomerRepository.ListSalesAsync(tenant.Id, customer.Id);

            proposals.Should().HaveCount(2);
            proposals[0].Date.Should().Be(new DateOnly(2026, 8, 6));
            proposals[0].VehicleCode.Should().Be(secondVehicleCode);
            proposals[0].Brand.Should().Be("Fiat");

            sales.Should().ContainSingle().Which.VehicleCode.Should().Be(secondVehicleCode);
            sales[0].HadTradeIn.Should().BeFalse();
        }

        private async Task RunTheInitializerAsync()
        {
            using var scope = api.Scope();
            await scope.ServiceProvider.GetRequiredService<DbInitializer>().RunAsync();
        }

        private async Task<Guid> CarAsync()
        {
            var suffix = Random.Shared.Next(100, 999);

            var vehicle = await ReadDataAsync(await mine.PostAsJsonAsync(Url("/api/vehicles"), new
            {
                plate = $"CLI{suffix % 10}B{suffix % 100:00}",
                chassis = $"9BWC1R{suffix:000}B0000{Random.Shared.Next(100, 999):000}",
                brand = "Fiat",
                model = "Uno",
                modelYear = 2014,
                manufactureYear = 2013,
                mileage = 90_000,
                fuelType = 1,
                transmission = 1,
                origin = 1,
                purchasePrice = 15_000,
                purchaseDate = "2026-08-01",
            }));

            return vehicle.GetProperty("code").GetGuid();
        }

        private static async Task<JsonElement> ReadDataAsync(HttpResponseMessage answer)
        {
            answer.IsSuccessStatusCode.Should().BeTrue(await answer.Content.ReadAsStringAsync());

            var body = await answer.Content.ReadFromJsonAsync<JsonElement>();

            return body.GetProperty("data");
        }

        private static Uri Url(string path) => new(path, UriKind.Relative);
    }
}
