using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace RevendaPro.Tests.Integration
{
    /// <summary>
    /// A lixeira (M23), contra o MariaDB de verdade.
    ///
    /// O que se prova: o carro apagado aparece com o dia e o nome de quem apagou; o gasto
    /// apagado aparece com o carro dele, e dizendo que esse carro também está na lixeira — que
    /// é a ordem em que as duas coisas voltam; devolver o carro traz a ficha inteira sem
    /// ressuscitar o que foi apagado à parte; a placa cadastrada de novo recusa a volta e diz
    /// de quem ela é; e a outra revenda enxerga nada, nem devolve nada.
    /// </summary>
    [Collection(ApiCollection.Name)]
    public class TrashTests(ApiFixture api) : IAsyncLifetime
    {
        private HttpClient mine = default!;
        private SecondDealership other = default!;
        private Guid vehicleCode;
        private Guid expenseCode;
        private Guid keptExpenseCode;
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

            // Um segundo gasto que ninguém apagou: ele é o que prova que devolver o carro
            // devolve a ficha inteira, sem a volta precisar percorrer filho nenhum.
            var kept = await ReadDataAsync(await mine.PostAsJsonAsync(
                Url($"/api/vehicles/{vehicleCode}/expenses"),
                new
                {
                    vehicleCode,
                    expenseTypeCode = types[0].GetProperty("code").GetGuid(),
                    description = "Revisão completa",
                    amount = 890,
                    date = "2026-08-25",
                    isPaid = true,
                }));

            keptExpenseCode = kept.GetProperty("code").GetGuid();

            // O engano que a lixeira existe para desfazer: o gasto primeiro, o carro depois.
            await mine.DeleteAsync(Url($"/api/vehicles/{vehicleCode}/expenses/{expenseCode}"));
            await mine.DeleteAsync(Url($"/api/vehicles/{vehicleCode}"));
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Os testes que devolvem o carro o deixam no pátio, e o pátio é compartilhado com
        /// quem afirma que ele começa vazio. Apagar de novo devolve a pilha ao estado em que
        /// ela foi encontrada; num carro que já está na lixeira, o DELETE responde 404 e nada
        /// acontece.
        /// </remarks>
        public async Task DisposeAsync()
        {
            if (vehicleCode != Guid.Empty)
            {
                await mine.DeleteAsync(Url($"/api/vehicles/{vehicleCode}"));
            }
        }

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

        [Fact]
        public async Task DevolverOCarro_TrazAFichaInteiraDeVolta_ESemRessuscitarOQueFoiApagadoAParte()
        {
            var restored = await mine.PostAsync(Url($"/api/trash/1/{vehicleCode}/restore"), null);

            restored.StatusCode.Should().Be(HttpStatusCode.NoContent);

            var vehicle = await ReadDataAsync(await mine.GetAsync(Url($"/api/vehicles/{vehicleCode}")));
            vehicle.GetProperty("plate").GetString().Should().Be(plate, "o carro voltou ao pátio");

            var expenses = await ReadDataAsync(
                await mine.GetAsync(Url($"/api/vehicles/{vehicleCode}/expenses")));

            var codes = expenses.EnumerateArray()
                .Select(expense => expense.GetProperty("code").GetGuid())
                .ToList();

            codes.Should().Contain(keptExpenseCode,
                "a ficha volta inteira: o gasto sumiu porque a consulta dele passa pelo carro");

            codes.Should().NotContain(expenseCode,
                "e o que alguém apagou à parte continua apagado, senão a volta ressuscitaria "
                + "a foto tirada da ficha de propósito na semana passada");

            // O custo do carro é somado a cada leitura desde o M6: com a ficha de volta, ele
            // volta a contar o gasto que ficou.
            vehicle.GetProperty("cost").GetProperty("total").GetDecimal().Should().Be(22_890m);
        }

        [Fact]
        public async Task DevolverUmCarroCujaPlacaFoiCadastradaDeNovo_Recusa422_EDizQualCarroEstaComEla()
        {
            var novo = await ReadDataAsync(await mine.PostAsJsonAsync(Url("/api/vehicles"), new
            {
                plate,
                chassis = $"9BWZ2R{Random.Shared.Next(100, 999):000}D0000{Random.Shared.Next(100, 999):000}",
                brand = "Fiat",
                model = "Uno",
                modelYear = 2015,
                manufactureYear = 2014,
                mileage = 60_000,
                fuelType = 1,
                transmission = 1,
                origin = 1,
                purchasePrice = 18_000,
                purchaseDate = "2026-09-01",
            }));

            try
            {
                var refused = await mine.PostAsync(Url($"/api/trash/1/{vehicleCode}/restore"), null);

                refused.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

                var body = await refused.Content.ReadAsStringAsync();

                body.Should().Contain(plate).And.Contain("Fiat Uno 2015",
                    "a recusa nomeia o culpado: sem isso a pessoa fica sem saída");
            }
            finally
            {
                await mine.DeleteAsync(Url($"/api/vehicles/{novo.GetProperty("code").GetGuid()}"));
            }
        }

        [Fact]
        public async Task OGastoSoVoltaDepoisDoCarro()
        {
            var cedo = await mine.PostAsync(Url($"/api/trash/2/{expenseCode}/restore"), null);

            cedo.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

            var body = await cedo.Content.ReadAsStringAsync();

            body.Should().Contain("Volkswagen Fox").And.Contain("Devolva o carro primeiro",
                "um gasto devolvido a um carro excluído voltaria ativo no banco e ausente de "
                + "toda tela");

            await mine.PostAsync(Url($"/api/trash/1/{vehicleCode}/restore"), null);

            var agora = await mine.PostAsync(Url($"/api/trash/2/{expenseCode}/restore"), null);

            agora.StatusCode.Should().Be(HttpStatusCode.NoContent);

            var expenses = await ReadDataAsync(
                await mine.GetAsync(Url($"/api/vehicles/{vehicleCode}/expenses")));

            expenses.EnumerateArray()
                .Select(expense => expense.GetProperty("code").GetGuid())
                .Should().Contain(expenseCode);
        }

        [Fact]
        public async Task AOutraRevenda_JamaisDevolveOQueEDaMinha()
        {
            var hers = await api.AsAsync(other.AdminEmail);

            var carro = await hers.PostAsync(Url($"/api/trash/1/{vehicleCode}/restore"), null);
            var gasto = await hers.PostAsync(Url($"/api/trash/2/{expenseCode}/restore"), null);

            carro.StatusCode.Should().Be(HttpStatusCode.NotFound);
            gasto.StatusCode.Should().Be(HttpStatusCode.NotFound);

            // E o carro continua onde estava: uma recusa que já tivesse escrito seria pior do
            // que uma que passa.
            var trash = await ReadDataAsync(await mine.GetAsync(Url("/api/trash?kind=1")));

            trash.EnumerateArray().Should().Contain(
                item => item.GetProperty("code").GetGuid() == vehicleCode);
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
