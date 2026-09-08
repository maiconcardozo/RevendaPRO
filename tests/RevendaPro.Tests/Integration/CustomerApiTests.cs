using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RevendaPro.Domain.Interfaces;

namespace RevendaPro.Tests.Integration
{
    /// <summary>
    /// A API dos clientes (M21), contra o MariaDB de verdade: a proposta cria o cliente em linha
    /// e reaproveita pelo telefone; a venda que fecha a proposta é do mesmo cliente e traz o CPF;
    /// documento igual é recusa e telefone igual é aviso; excluir quem tem história é recusado;
    /// a outra revenda enxerga nada; e a planilha sai.
    /// </summary>
    [Collection(ApiCollection.Name)]
    public class CustomerApiTests(ApiFixture api) : IAsyncLifetime
    {
        private HttpClient mine = default!;
        private SecondDealership other = default!;
        private readonly List<Guid> vehicles = [];
        private readonly List<Guid> customers = [];
        private string phone = string.Empty;

        /// <inheritdoc/>
        public async Task InitializeAsync()
        {
            mine = await api.ClientOfAsync("admin@revendapro.local");
            other = await api.OtherDealershipAsync();
            phone = $"518{Random.Shared.Next(10_000_000, 99_999_999)}";
        }

        /// <inheritdoc/>
        public async Task DisposeAsync()
        {
            foreach (var code in vehicles)
            {
                await mine.DeleteAsync(Url($"/api/vehicles/{code}"));
            }

            using var scope = api.Scope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var tenant = (await unitOfWork.TenantRepository.GetFirstAsync())!;

            foreach (var code in customers)
            {
                var customer = await unitOfWork.CustomerRepository.GetByCodeAsync(tenant.Id, code);

                if (customer is not null)
                {
                    unitOfWork.CustomerRepository.Remove(customer, "tests");
                }
            }

            await unitOfWork.CommitAsync();
        }

        [Fact]
        public async Task TwoProposalsWithTheSamePhone_AreOneCustomer_AndTheSaleBringsTheDocument()
        {
            var car = await CarAsync();
            var otherCar = await CarAsync();

            var first = await ReadDataAsync(await mine.PostAsJsonAsync(Url($"/api/vehicles/{car}/proposals"), new
            {
                prospectName = "Marcos Silva",
                prospectPhone = $"({phone[..2]}) {phone[2..7]}-{phone[7..]}",
                amount = 20_000,
                date = "2026-08-05",
                paymentMethod = 1,
                channel = 1,
            }));

            var customerCode = first.GetProperty("customerCode").GetGuid();
            customers.Add(customerCode);
            first.GetProperty("prospectPhone").GetString().Should().Be(phone, "o telefone vai só em dígitos");

            var second = await ReadDataAsync(await mine.PostAsJsonAsync(Url($"/api/vehicles/{otherCar}/proposals"), new
            {
                prospectName = "marcos silva",
                prospectPhone = phone,
                amount = 21_000,
                date = "2026-08-06",
                paymentMethod = 1,
                channel = 1,
            }));

            second.GetProperty("customerCode").GetGuid().Should().Be(customerCode, "o mesmo telefone é a mesma pessoa");
            second.GetProperty("prospectName").GetString().Should().Be("Marcos Silva", "a tela lê o nome do cliente, e não o digitado");

            var listed = await ReadDataAsync(await mine.GetAsync(Url($"/api/customers?search={phone[3..8]}")));
            var line = listed.EnumerateArray().Single(c => c.GetProperty("code").GetGuid() == customerCode);
            line.GetProperty("proposalCount").GetInt32().Should().Be(2);
            line.GetProperty("saleCount").GetInt32().Should().Be(0);

            foreach (var status in new[] { 2, 4 })
            {
                await mine.PatchAsJsonAsync(Url($"/api/vehicles/{otherCar}/status"), new { status, reason = "teste" });
            }

            var sale = await ReadDataAsync(await mine.PostAsJsonAsync(Url($"/api/vehicles/{otherCar}/sale"), new
            {
                proposalCode = second.GetProperty("code").GetGuid(),
                date = "2026-08-07",
                amount = 21_000,
                paymentMethod = 1,
                channel = 1,
                commission = 0,
                buyerName = "Marcos Silva",
                buyerDocument = "390.533.447-05",
                buyerPhone = phone,
            }));

            sale.GetProperty("customerCode").GetGuid().Should().Be(customerCode, "a venda que fecha a proposta é do cliente dela");

            var detail = await ReadDataAsync(await mine.GetAsync(Url($"/api/customers/{customerCode}")));
            detail.GetProperty("customer").GetProperty("document").GetString().Should().Be("39053344705", "o CPF da venda completou o cadastro");
            detail.GetProperty("customer").GetProperty("saleCount").GetInt32().Should().Be(1);
            detail.GetProperty("customer").GetProperty("boughtTotal").GetDecimal().Should().Be(21_000m);
            detail.GetProperty("proposals").GetArrayLength().Should().Be(2);
            detail.GetProperty("sales").GetArrayLength().Should().Be(1);
            detail.GetProperty("sales")[0].GetProperty("vehicleName").GetString().Should().Be("Fiat Uno");

            var sales = await ReadDataAsync(await mine.GetAsync(Url("/api/sales?from=2026-08-07&to=2026-08-07")));
            sales.EnumerateArray()
                .Single(s => s.GetProperty("vehicleCode").GetGuid() == otherCar)
                .GetProperty("customerCode").GetGuid().Should().Be(customerCode);

            var refused = await mine.DeleteAsync(Url($"/api/customers/{customerCode}"));
            refused.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, "quem tem história fica");
            (await DetailOf(refused)).Should().Contain("2 propostas").And.Contain("1 compra");
        }

        [Fact]
        public async Task SavingACustomer_RefusesTheSameDocument_AndWarnsAboutTheSamePhone()
        {
            var created = await ReadDataAsync(await mine.PostAsJsonAsync(Url("/api/customers"), new
            {
                name = "Helena Duarte",
                document = "123.456.789-09",
                phone,
                email = "Helena@Mail.com",
            }));

            var code = created.GetProperty("code").GetGuid();
            customers.Add(code);
            created.GetProperty("email").GetString().Should().Be("helena@mail.com");

            var sameDocument = await mine.PostAsJsonAsync(Url("/api/customers"), new
            {
                name = "Outra Pessoa",
                document = "12345678909",
            });

            sameDocument.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            (await DetailOf(sameDocument)).Should().Contain("já é de Helena Duarte");

            var samePhone = await mine.PostAsJsonAsync(Url("/api/customers"), new
            {
                name = "Irmão da Helena",
                phone,
            });

            samePhone.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, "telefone igual é aviso");
            (await DetailOf(samePhone)).Should().Contain("confirme que é outra pessoa");

            var confirmed = await ReadDataAsync(await mine.PostAsJsonAsync(Url("/api/customers"), new
            {
                name = "Irmão da Helena",
                phone,
                confirmSamePhone = true,
            }));

            customers.Add(confirmed.GetProperty("code").GetGuid());

            var invalid = await mine.PostAsJsonAsync(Url("/api/customers"), new
            {
                name = "Documento errado",
                document = "111.111.111-11",
            });

            invalid.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

            var deleted = await mine.DeleteAsync(Url($"/api/customers/{confirmed.GetProperty("code").GetGuid()}"));
            deleted.StatusCode.Should().Be(HttpStatusCode.NoContent, "sem história, sai");
        }

        [Fact]
        public async Task TheOtherDealership_SeesNothing_AndTheSpreadsheetComesOut()
        {
            var created = await ReadDataAsync(await mine.PostAsJsonAsync(Url("/api/customers"), new
            {
                name = "Cristiano Bueno",
                phone,
            }));

            var code = created.GetProperty("code").GetGuid();
            customers.Add(code);

            var hers = await api.AsAsync(other.AdminEmail);

            var theirs = await hers.GetAsync(Url($"/api/customers/{code}"));
            theirs.StatusCode.Should().Be(HttpStatusCode.NotFound);

            var theirList = await ReadDataAsync(await hers.GetAsync(Url($"/api/customers?search={phone}")));
            theirList.GetArrayLength().Should().Be(0);

            var csv = await mine.GetAsync(Url($"/api/exports/customers?format=csv&search={phone}"));
            csv.StatusCode.Should().Be(HttpStatusCode.OK);
            csv.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");

            var text = Encoding.UTF8.GetString(await csv.Content.ReadAsByteArrayAsync());
            text.Should().Contain("Cristiano Bueno").And.Contain($"({phone[..2]}) {phone[2..7]}-{phone[7..]}");
        }

        private async Task<Guid> CarAsync()
        {
            var suffix = Random.Shared.Next(100, 999);

            var vehicle = await ReadDataAsync(await mine.PostAsJsonAsync(Url("/api/vehicles"), new
            {
                plate = $"CLI{suffix % 10}C{suffix % 100:00}",
                chassis = $"9BWC2R{suffix:000}C0000{Random.Shared.Next(100, 999):000}",
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

            var code = vehicle.GetProperty("code").GetGuid();
            vehicles.Add(code);

            return code;
        }

        private static async Task<JsonElement> ReadDataAsync(HttpResponseMessage answer)
        {
            answer.IsSuccessStatusCode.Should().BeTrue(await answer.Content.ReadAsStringAsync());

            var body = await answer.Content.ReadFromJsonAsync<JsonElement>();

            return body.GetProperty("data");
        }

        /// <summary>O motivo da recusa, já sem o escape de acentos do JSON.</summary>
        private static async Task<string> DetailOf(HttpResponseMessage answer)
        {
            var body = await answer.Content.ReadFromJsonAsync<JsonElement>();

            return body.GetProperty("detail").GetString() ?? string.Empty;
        }

        private static Uri Url(string path) => new(path, UriKind.Relative);
    }
}
