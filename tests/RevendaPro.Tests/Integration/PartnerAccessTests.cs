using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace RevendaPro.Tests.Integration
{
    /// <summary>
    /// A fronteira do parceiro (M24), com a API no ar.
    ///
    /// É a primeira fronteira de segurança <b>dentro</b> da mesma empresa, e por isso ela é
    /// provada do mesmo jeito que a fronteira entre empresas foi provada no M12: chamando de
    /// verdade, com um parceiro de verdade, e conferindo o que ele alcança — e sobretudo o que
    /// ele deixa de alcançar.
    ///
    /// A lista de recusas é <b>escrita à mão</b>, de propósito. A do middleware é uma permissão
    /// explícita, e derivar a expectativa dela seria escrever o teste com a mesma frase que ele
    /// deveria conferir: os dois ficariam verdes juntos no dia em que a lista crescesse por
    /// engano. É a mesma razão da segunda lista do M12.
    /// </summary>
    [Collection(ApiCollection.Name)]
    public class PartnerAccessTests(ApiFixture api) : IAsyncLifetime
    {
        private HttpClient boss = default!;
        private HttpClient partner = default!;
        private Guid myYard;
        private Guid otherYard;
        private Guid myCar;
        private Guid otherCar;
        private string partnerEmail = string.Empty;
        private Guid partnerCode;
        private Guid partnerRole;
        private string plate = string.Empty;

        /// <inheritdoc/>
        public async Task InitializeAsync()
        {
            boss = await api.ClientOfAsync("admin@revendapro.local");

            var tag = Guid.NewGuid().ToString("N")[..6];

            myYard = (await ReadDataAsync(await boss.PostAsJsonAsync(Url("/api/yards"), new
            {
                name = $"Loja do Joãozinho {tag}",
                kind = 2,
            }))).GetProperty("code").GetGuid();

            otherYard = (await ReadDataAsync(await boss.PostAsJsonAsync(Url("/api/yards"), new
            {
                name = $"Pátio da revenda {tag}",
                kind = 1,
            }))).GetProperty("code").GetGuid();

            myCar = await CarAsync(myYard, "PTA");
            otherCar = await CarAsync(otherYard, "PTB");

            var roles = await ReadDataAsync(await boss.GetAsync(Url("/api/roles")));
            var parceiro = roles.EnumerateArray()
                .Single(role => role.GetProperty("name").GetString() == "Parceiro");

            partnerRole = parceiro.GetProperty("code").GetGuid();
            partnerEmail = $"joaozinho.{tag}@revendapro.local";

            var created = await ReadDataAsync(await boss.PostAsJsonAsync(Url("/api/users"), new
            {
                name = "Joãozinho da Loja",
                email = partnerEmail,
                password = ApiFixture.SharedPassword,
                isBlocked = false,
                roles = new[] { partnerRole },
                document = "39053344705",
                phone = "51999990000",
            }));

            partnerCode = created.GetProperty("code").GetGuid();

            // Ele entra ANTES de ser preso ao pátio: o token que ele carrega daqui em diante é
            // o de quem enxergava tudo. Se a restrição viajasse numa claim, ele continuaria
            // enxergando tudo por até quinze minutos — e todo teste abaixo passaria em falso.
            partner = await api.AsAsync(partnerEmail);

            await BindAsync(myYard);
        }

        /// <inheritdoc/>
        public async Task DisposeAsync()
        {
            foreach (var car in new[] { myCar, otherCar })
            {
                await boss.DeleteAsync(Url($"/api/vehicles/{car}"));
            }

            await boss.DeleteAsync(Url($"/api/users/{partnerCode}"));

            foreach (var yard in new[] { myYard, otherYard })
            {
                await boss.DeleteAsync(Url($"/api/yards/{yard}"));
            }
        }

        [Fact]
        public async Task OParceiro_EnxergaSoOsCarrosDoPatioDele()
        {
            var mine = await ReadDataAsync(await partner.GetAsync(Url("/api/vehicles")));

            var codes = mine.EnumerateArray()
                .Select(car => car.GetProperty("code").GetGuid())
                .ToList();

            codes.Should().Contain(myCar);
            codes.Should().NotContain(otherCar, "o carro do outro pátio jamais é dele");

            codes.Should().OnlyContain(
                code => code == myCar,
                "o pátio dele tem um carro só, e o estoque da revenda tem muitos");
        }

        [Fact]
        public async Task OFiltroDeOutroPatio_NaoOTiraDoPatioDele()
        {
            // A restrição vence o filtro: pedir o outro pátio devolve o dele, e jamais o outro.
            var asked = await ReadDataAsync(
                await partner.GetAsync(Url($"/api/vehicles?yard={otherYard}")));

            asked.EnumerateArray()
                .Select(car => car.GetProperty("code").GetGuid())
                .Should().NotContain(otherCar);
        }

        [Fact]
        public async Task OCarroDeOutroPatio_Responde404_EJamais403()
        {
            var answer = await partner.GetAsync(Url($"/api/vehicles/{otherCar}"));

            // 404 e jamais 403: para quem está preso a um lugar, o carro que está em outro
            // simplesmente não existe. Um 403 confirmaria que aquele código é de um carro de
            // verdade, e ele enumeraria o estoque inteiro contando as recusas.
            answer.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task AFichaDoParceiro_TrazOCarro_ESemODinheiroDaCasa()
        {
            var car = await ReadDataAsync(await partner.GetAsync(Url($"/api/vehicles/{myCar}")));

            car.GetProperty("plate").GetString().Should().Be(plate);
            car.GetProperty("advertisedPrice").GetDecimal().Should().Be(41_900m,
                "o preço anunciado é o que ele diz a quem pergunta do carro");

            foreach (var hidden in new[]
                     {
                         "purchasePrice", "supplierName", "budgetCeiling",
                         "desiredNetPrice", "minimumNetPrice", "cost", "notes",
                     })
            {
                car.GetProperty(hidden).ValueKind.Should().Be(
                    JsonValueKind.Null,
                    "{0} é o poder de barganha da revenda, e o parceiro está do lado de fora",
                    hidden);
            }
        }

        [Fact]
        public async Task OMesmoCarro_ContinuaInteiroParaARevenda()
        {
            // Guarda deste arquivo: se a ficha viesse cortada para todo mundo, os testes acima
            // ficariam verdes provando nada.
            var car = await ReadDataAsync(await boss.GetAsync(Url($"/api/vehicles/{myCar}")));

            car.GetProperty("purchasePrice").GetDecimal().Should().Be(28_000m);
            car.GetProperty("cost").GetProperty("total").GetDecimal().Should().Be(28_000m);
        }

        [Fact]
        public async Task OParceiro_AlcancaAListaCurta_ERecusaTodoORestoCom403()
        {
            var refused = new List<string>();

            foreach (var (method, url) in Elsewhere())
            {
                var answer = await Call(partner, method, url);

                if (answer.StatusCode != HttpStatusCode.Forbidden)
                {
                    refused.Add($"{method} {url} respondeu {(int)answer.StatusCode}");
                }
            }

            refused.Should().BeEmpty(
                "quem enxerga um pátio alcança os carros que estão nele, e mais nada");
        }

        [Fact]
        public async Task SoltarOParceiro_ValeNaRequisicaoSeguinte()
        {
            await BindAsync(null);

            try
            {
                var all = await ReadDataAsync(await partner.GetAsync(Url("/api/vehicles")));

                all.EnumerateArray()
                    .Select(car => car.GetProperty("code").GetGuid())
                    .Should().Contain(otherCar,
                        "soltar alguém vale já, e jamais quando o token dele expirar");
            }
            finally
            {
                await BindAsync(myYard);
            }
        }

        /// <summary>
        /// O que o parceiro <b>jamais</b> alcança, dito em português e escrito à mão.
        ///
        /// Cada linha é uma coisa da casa: o dinheiro, as pessoas, os fornecedores, os papéis,
        /// e toda escrita. Uma tela que aparecer amanhã entra recusada, porque a lista do
        /// middleware é uma permissão explícita — e este teste é o que percebe se alguém a
        /// abrir sem querer.
        /// </summary>
        private IEnumerable<(string Method, string Url)> Elsewhere() =>
        [
            ("GET", "/api/dashboard"),
            ("GET", "/api/market"),
            ("GET", "/api/cashflow"),
            ("GET", "/api/sales"),
            ("GET", "/api/customers"),
            ("GET", "/api/suppliers"),
            ("GET", "/api/expense-types"),
            ("GET", "/api/yards"),
            ("GET", "/api/users"),
            ("GET", "/api/roles"),
            ("GET", "/api/company"),
            ("GET", "/api/trash?kind=1"),
            ("GET", "/api/exports/vehicles"),
            ("GET", "/api/exports/expenses"),

            // A ficha do carro dele, e ainda assim recusada: exportar é decisão seguinte.
            ("GET", $"/api/vehicles/{myCar}/reports/sale-sheet"),

            // As abas da ficha, que são o dinheiro e a negociação da casa.
            ("GET", $"/api/vehicles/{myCar}/expenses"),
            ("GET", $"/api/vehicles/{myCar}/documents"),
            ("GET", $"/api/vehicles/{myCar}/timeline"),
            ("GET", $"/api/vehicles/{myCar}/proposals"),

            // E toda escrita, inclusive no carro que está com ele: o parceiro lê, e escreve nada.
            ("DELETE", $"/api/vehicles/{myCar}"),
            ("POST", "/api/vehicles"),
            ("PUT", $"/api/vehicles/{myCar}"),
        ];

        private static Task<HttpResponseMessage> Call(HttpClient client, string method, string url) =>
            client.SendAsync(new HttpRequestMessage(new HttpMethod(method), new Uri(url, UriKind.Relative))
            {
                Content = method is "POST" or "PUT"
                    ? JsonContent.Create(new { })
                    : null,
            });

        private async Task BindAsync(Guid? yard)
        {
            var answer = await boss.PutAsJsonAsync(Url($"/api/users/{partnerCode}"), new
            {
                name = "Joãozinho da Loja",
                email = partnerEmail,
                password = (string?)null,
                isBlocked = false,
                roles = new[] { partnerRole },
                document = "39053344705",
                phone = "51999990000",
                yardCode = yard,
            });

            answer.IsSuccessStatusCode.Should().BeTrue(await answer.Content.ReadAsStringAsync());
        }

        private async Task<Guid> CarAsync(Guid yard, string prefix)
        {
            var n = Random.Shared.Next(100, 999);
            var thisPlate = $"{prefix}{n % 10}D{n % 100:00}";

            var car = await ReadDataAsync(await boss.PostAsJsonAsync(Url("/api/vehicles"), new
            {
                plate = thisPlate,
                chassis = $"9BWP1R{n:000}D0000{Random.Shared.Next(100, 999):000}",
                brand = "Jeep",
                model = "Renegade",
                modelYear = 2020,
                manufactureYear = 2019,
                mileage = 50_000,
                fuelType = 1,
                transmission = 2,
                origin = 1,
                purchasePrice = 28_000,
                purchaseDate = "2026-07-01",
                advertisedPrice = 41_900,
                yardCode = yard,
            }));

            if (prefix == "PTA")
            {
                plate = thisPlate;
            }

            return car.GetProperty("code").GetGuid();
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
