using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace RevendaPro.Tests.Integration
{
    /// <summary>
    /// A lixeira (M23) enxergando, contra o MariaDB de verdade.
    ///
    /// O que se prova: o carro apagado aparece com o dia e o nome de quem apagou; o gasto
    /// apagado aparece com o carro dele, e dizendo que esse carro também está na lixeira — que
    /// é a ordem em que as duas coisas voltam; e a outra revenda enxerga nada.
    /// </summary>
    [Collection(ApiCollection.Name)]
    public class TrashTests(ApiFixture api) : IAsyncLifetime
    {
        private HttpClient mine = default!;
        private SecondDealership other = default!;
        private Guid vehicleCode;
        private Guid expenseCode;
        private string plate = string.Empty;

        /// <inheritdoc/>
        public async Task InitializeAsync()
        {
            mine = await api.ClientOfAsync("admin@revendapro.local");
            other = await api.OtherDealershipAsync();

            var suffix = Random.Shared.Next(100, 999);
            plate = $"LXA{suffix % 10}D{suffix % 100:00}";

            var vehicle = await ReadDataAsync(await mine.PostAsJsonAsync(Url("/api/vehicles"), new
            {
                plate,
                chassis = $"9BWZ1R{suffix:000}D0000{Random.Shared.Next(100, 999):000}",
                brand = "Volkswagen",
                model = "Fox",
                version = "1.6 Comfortline",
                modelYear = 2015,
                manufactureYear = 2014,
                mileage = 80_000,
                fuelType = 1,
                transmission = 1,
                origin = 1,
                purchasePrice = 22_000,
                purchaseDate = "2026-08-01",
            }));

            vehicleCode = vehicle.GetProperty("code").GetGuid();

            var types = await ReadDataAsync(await mine.GetAsync(Url("/api/expense-types?scope=1")));

            var expense = await ReadDataAsync(await mine.PostAsJsonAsync(
                Url($"/api/vehicles/{vehicleCode}/expenses"),
                new
                {
                    vehicleCode,
                    expenseTypeCode = types[0].GetProperty("code").GetGuid(),
                    description = "Troca da embreagem",
                    amount = 3_200,
                    date = "2026-08-20",
                    isPaid = true,
                }));

            expenseCode = expense.GetProperty("code").GetGuid();

            // O engano que a lixeira existe para desfazer: o gasto primeiro, o carro depois.
            await mine.DeleteAsync(Url($"/api/vehicles/{vehicleCode}/expenses/{expenseCode}"));
            await mine.DeleteAsync(Url($"/api/vehicles/{vehicleCode}"));
        }

        /// <inheritdoc/>
        public Task DisposeAsync() => Task.CompletedTask;

        [Fact]
        public async Task OCarroApagado_ApareceNaLixeira_ComODiaEONomeDeQuemApagou()
        {
            var items = await ReadDataAsync(await mine.GetAsync(Url("/api/trash?kind=1")));

            var fox = items.EnumerateArray()
                .Single(item => item.GetProperty("code").GetGuid() == vehicleCode);

            fox.GetProperty("kind").GetInt32().Should().Be(1);
            fox.GetProperty("title").GetString().Should().Be(plate, "é pela placa que se reconhece o carro");
            fox.GetProperty("subtitle").GetString().Should().Be("Volkswagen Fox 1.6 Comfortline 2015");
            fox.GetProperty("amount").GetDecimal().Should().Be(22_000m);

            fox.GetProperty("deletedAt").ValueKind.Should().NotBe(
                JsonValueKind.Null, "a lixeira existe para dizer quando a coisa sumiu");

            fox.GetProperty("deletedBy").GetString().Should().NotBeNullOrWhiteSpace(
                "e para a pergunta \"quem apagou o Fox?\" ter resposta");

            fox.GetProperty("vehicleCode").ValueKind.Should().Be(
                JsonValueKind.Null, "o carro é a coisa apagada, e a ficha dele ainda jamais abre");
        }

        [Fact]
        public async Task OGastoApagado_ApareceComOCarro_EDizendoQueOCarroTambemEstaNaLixeira()
        {
            var items = await ReadDataAsync(await mine.GetAsync(Url("/api/trash?kind=2")));

            var embreagem = items.EnumerateArray()
                .Single(item => item.GetProperty("code").GetGuid() == expenseCode);

            embreagem.GetProperty("kind").GetInt32().Should().Be(2);
            embreagem.GetProperty("title").GetString().Should().Be("Troca da embreagem");
            embreagem.GetProperty("amount").GetDecimal().Should().Be(3_200m);
            embreagem.GetProperty("date").GetString().Should().Be("2026-08-20");
            embreagem.GetProperty("vehiclePlate").GetString().Should().Be(plate);
            embreagem.GetProperty("vehicleName").GetString().Should().Be("Volkswagen Fox");

            embreagem.GetProperty("vehicleIsInYard").GetBoolean().Should().BeFalse(
                "o carro dele também foi apagado, e é isso que diz à pessoa em que ordem devolver");
        }

        [Fact]
        public async Task ALixeiraOrdenaDaExclusaoMaisRecenteParaAMaisAntiga()
        {
            var items = await ReadDataAsync(await mine.GetAsync(Url("/api/trash?kind=1")));

            var moments = items.EnumerateArray()
                .Select(item => item.GetProperty("deletedAt").GetDateTime())
                .ToList();

            moments.Should().BeInDescendingOrder("o engano de hoje é o que se procura");
        }

        [Fact]
        public async Task ALixeiraDaOutraRevenda_EnxergaNada()
        {
            var hers = await api.AsAsync(other.AdminEmail);

            foreach (var kind in new[] { 1, 2, 3 })
            {
                var items = await ReadDataAsync(await hers.GetAsync(Url($"/api/trash?kind={kind}")));

                items.EnumerateArray().Should().NotContain(
                    item => item.GetProperty("code").GetGuid() == vehicleCode
                            || item.GetProperty("code").GetGuid() == expenseCode,
                    "a lixeira de uma loja jamais mostra o que a outra apagou (RNF-04)");
            }
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
