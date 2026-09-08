using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace RevendaPro.Tests.Integration
{
    /// <summary>
    /// Quanto já foi para cada fornecedor, pela API e contra o banco de verdade.
    ///
    /// A soma por fornecedor é feita pelo banco (decisão 5 do M18), e uma soma em SQL só se
    /// prova com SQL: o teste de unidade confere o que a aplicação faz com a linha, e este
    /// confere que a linha chega — com o tipo que o driver entrega, o pago separado do previsto,
    /// e sem nenhum centavo da outra revenda.
    /// </summary>
    [Collection(ApiCollection.Name)]
    public class SupplierSpendingTests(ApiFixture api) : IAsyncLifetime
    {
        private HttpClient mine = default!;
        private SecondDealership other = default!;
        private Guid supplierCode;
        private Guid vehicleCode;

        /// <inheritdoc/>
        public async Task InitializeAsync()
        {
            other = await api.OtherDealershipAsync();
            mine = await api.ClientOfAsync("admin@revendapro.local");

            var segments = await ReadDataAsync(await mine.GetAsync(Url("/api/supplier-segments")));
            var segmentCode = segments[0].GetProperty("code").GetGuid();

            var supplier = await ReadDataAsync(await mine.PostAsJsonAsync(Url("/api/suppliers"), new
            {
                name = $"Funilaria do Zé {Guid.NewGuid():N}",
                segmentCode,
            }));

            supplierCode = supplier.GetProperty("code").GetGuid();

            var suffix = Random.Shared.Next(100, 999);

            var vehicle = await ReadDataAsync(await mine.PostAsJsonAsync(Url("/api/vehicles"), new
            {
                plate = $"FOR{suffix % 10}A{suffix % 100:00}",
                chassis = $"9BWF1R{suffix:000}A0000{suffix:000}",
                brand = "Fiat",
                model = "Palio",
                modelYear = 2013,
                manufactureYear = 2012,
                mileage = 120_000,
                fuelType = 1,
                transmission = 1,
                origin = 1,
                purchasePrice = 18_000,
                purchaseDate = "2026-08-01",
            }));

            vehicleCode = vehicle.GetProperty("code").GetGuid();

            var types = await ReadDataAsync(await mine.GetAsync(Url("/api/expense-types")));
            var typeCode = types[0].GetProperty("code").GetGuid();

            await ExpenseAsync(typeCode, "Pintura do capô", 1_400m, "2026-08-10", isPaid: true);
            await ExpenseAsync(typeCode, "Funilaria da lateral", 2_600m, "2026-08-20", isPaid: false);
        }

        /// <summary>
        /// O carro sai do pátio ao fim de cada teste: outros arquivos desta coleção provam que a
        /// revenda piloto nasce vazia, e um carro esquecido aqui faria a prova deles falhar.
        /// </summary>
        public async Task DisposeAsync()
        {
            if (vehicleCode != Guid.Empty)
            {
                await mine.DeleteAsync(Url($"/api/vehicles/{vehicleCode}"));
            }
        }

        [Fact]
        public async Task TheRanking_SumsThePaid_AndKeepsThePlannedApart()
        {
            var ranking = await ReadDataAsync(await mine.GetAsync(Url("/api/suppliers/spending")));

            var line = ranking.EnumerateArray()
                .Single(row => row.GetProperty("code").GetGuid() == supplierCode);

            line.GetProperty("paidTotal").GetDecimal().Should().Be(1_400m);
            line.GetProperty("plannedTotal").GetDecimal().Should().Be(2_600m);
            line.GetProperty("expenseCount").GetInt32().Should().Be(2);
            line.GetProperty("lastDate").GetString().Should().StartWith("2026-08-20");
        }

        [Fact]
        public async Task ThePeriod_IsReadOverTheDateOfTheExpense()
        {
            var ranking = await ReadDataAsync(
                await mine.GetAsync(Url("/api/suppliers/spending?from=2026-08-15&to=2026-08-31")));

            var line = ranking.EnumerateArray()
                .Single(row => row.GetProperty("code").GetGuid() == supplierCode);

            // Só a funilaria de 20/08 cabe no período, e ela é prevista: pago fica em zero.
            line.GetProperty("paidTotal").GetDecimal().Should().Be(0m);
            line.GetProperty("expenseCount").GetInt32().Should().Be(1);
        }

        [Fact]
        public async Task TheStatement_ListsEachExpense_WithThePlateOfTheCar()
        {
            var statement = await ReadDataAsync(
                await mine.GetAsync(Url($"/api/suppliers/{supplierCode}/expenses")));

            statement.GetProperty("paidTotal").GetDecimal().Should().Be(1_400m);
            statement.GetProperty("plannedTotal").GetDecimal().Should().Be(2_600m);
            statement.GetProperty("byType").GetArrayLength().Should().Be(1);

            var expenses = statement.GetProperty("expenses").EnumerateArray().ToList();
            expenses.Should().HaveCount(2);
            expenses[0].GetProperty("description").GetString().Should().Be("Funilaria da lateral");
            expenses[0].GetProperty("isPaid").GetBoolean().Should().BeFalse();
            expenses[0].GetProperty("vehicleCode").GetGuid().Should().Be(vehicleCode);
            expenses[0].GetProperty("vehicleName").GetString().Should().Be("Fiat Palio 2013");
        }

        [Fact]
        public async Task TheDashboard_RanksTheSupplier_InTheSamePeriodAsTheSales()
        {
            var dashboard = await ReadDataAsync(
                await mine.GetAsync(Url("/api/dashboard?from=2026-08-01&to=2026-08-31")));

            var line = dashboard.GetProperty("bySupplier").EnumerateArray()
                .Single(row => row.GetProperty("code").GetGuid() == supplierCode);

            line.GetProperty("paidTotal").GetDecimal().Should().Be(1_400m);
            dashboard.GetProperty("suppliersTotal").GetDecimal().Should().BeGreaterThanOrEqualTo(1_400m);
            dashboard.GetProperty("supplierCount").GetInt32().Should().BeGreaterThanOrEqualTo(1);
        }

        [Fact]
        public async Task TheOtherDealership_SeesNeitherTheRankingLine_NorTheStatement()
        {
            var hers = await api.AsAsync(other.AdminEmail);

            var ranking = await ReadDataAsync(await hers.GetAsync(Url("/api/suppliers/spending")));
            ranking.EnumerateArray()
                .Should().NotContain(row => row.GetProperty("code").GetGuid() == supplierCode);

            var statement = await hers.GetAsync(Url($"/api/suppliers/{supplierCode}/expenses"));
            statement.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        private async Task ExpenseAsync(Guid typeCode, string description, decimal amount, string date, bool isPaid)
        {
            var answer = await mine.PostAsJsonAsync(Url($"/api/vehicles/{vehicleCode}/expenses"), new
            {
                expenseTypeCode = typeCode,
                supplierCode,
                description,
                amount,
                date,
                isPaid,
            });

            answer.StatusCode.Should().Be(HttpStatusCode.OK, await answer.Content.ReadAsStringAsync());
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
