using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RevendaPro.Domain.ValueObjects;
using RevendaPro.Infrastructure.Reference;
using RevendaPro.Shared.Settings;

namespace RevendaPro.Tests.Unit
{
    /// <summary>
    /// A leitura da tabela de referência, com as respostas de verdade gravadas.
    ///
    /// As respostas abaixo foram capturadas da fonte em 3 de setembro de 2026, e é justamente
    /// por isso que elas valem: o preço chega como <c>"R$ 56.530,00"</c> e o mês como
    /// <c>"setembro de 2026"</c> num endpoint e <c>"setembro/2026"</c> no outro — a mesma API
    /// escrevendo o mesmo mês de dois jeitos. Nada aqui toca a rede.
    ///
    /// O que estes testes seguram é a promessa do M11: a tabela jamais derruba a operação. Fora
    /// do ar, estourada de limite ou respondendo num formato novo, ela devolve um resultado que
    /// o chamador sabe tratar, e nunca uma exceção solta.
    /// </summary>
    public class FipeCatalogTests
    {
        private const string PriceBody = """
            {
              "vehicleType": 1,
              "price": "R$ 56.530,00",
              "brand": "GM - Chevrolet",
              "model": "CRUZE LT 1.8 16V FlexPower 4p Aut.",
              "modelYear": 2014,
              "fuel": "Flex",
              "codeFipe": "004380-0",
              "referenceMonth": "setembro de 2026",
              "fuelAcronym": "F"
            }
            """;

        private const string ReferencesBody = """
            [
              { "code": 337, "month": "setembro/2026" },
              { "code": 336, "month": "agosto/2026" },
              { "code": 335, "month": "julho/2026" }
            ]
            """;

        private const string YearsBody = """
            [
              { "code": "2016-5", "name": "2016 Flex" },
              { "code": "2015-5", "name": "2015 Flex" },
              { "code": "2014-5", "name": "2014 Flex" }
            ]
            """;

        [Fact]
        public async Task ThePrice_ArrivesAsText_AndBecomesDecimalWithItsCents()
        {
            var catalog = Catalog(Answer(PriceBody));

            var read = await catalog.GetPriceAsync("004380-0", "2014-5", 337);

            read.Ok.Should().BeTrue();

            // Decimal, e jamais double: o centavo é dinheiro (RNF-12).
            read.Value!.Value.Should().Be(56_530.00m);
            read.Value.Reference.Should().Be(new DateOnly(2026, 9, 1));
            read.Value.Model.Should().Be("CRUZE LT 1.8 16V FlexPower 4p Aut.");
            read.Value.ModelYear.Should().Be(2014);
        }

        [Fact]
        public async Task TheNewestTable_IsTheOneWithTheHighestCode()
        {
            var catalog = Catalog(Answer(ReferencesBody));

            var read = await catalog.GetCurrentReferenceAsync();

            read.Ok.Should().BeTrue();

            // O código é o que fixa toda consulta seguinte, e ele cresce de um em um por mês:
            // ordenar por ele é mais firme do que confiar na ordem da lista.
            read.Value!.Code.Should().Be(337);
            read.Value.Month.Should().Be(new DateOnly(2026, 9, 1));
        }

        [Fact]
        public async Task EveryQuery_PinsTheTableAsked_AndCarriesTheTokenWhenThereIsOne()
        {
            var handler = Answer(PriceBody);
            var catalog = Catalog(handler, token: "segredo");

            await catalog.GetPriceAsync("004380-0", "2014-5", 336);

            // A URL inteira, e não um pedaço: é ela que precisa bater com a fonte de verdade.
            // Sem fixar a tabela, a mesma fonte devolveu agosto por um caminho e setembro por
            // outro, com valores diferentes.
            handler.LastPath.Should().Be(
                "https://fonte.exemplo/api/v2/cars/004380-0/years/2014-5?reference=336");

            handler.LastToken.Should().Be("segredo");
        }

        [Fact]
        public async Task WithoutToken_NoHeaderIsSent()
        {
            var handler = Answer(PriceBody);
            var catalog = Catalog(handler);

            await catalog.GetPriceAsync("004380-0", "2014-5", 337);

            handler.LastToken.Should().BeNull();
        }

        [Fact]
        public async Task ACarOutsideTheTable_IsMissing_AndNeverAFailure()
        {
            var catalog = Catalog(new FakeHandler(HttpStatusCode.NotFound, ""));

            var read = await catalog.GetPriceAsync("999999-9", "2014-5", 337);

            // Carro importado, muito antigo ou que a tabela nunca precificou é caso real, e é
            // fato final: tentar de novo em uma hora responde o mesmo.
            read.Outcome.Should().Be(FipeOutcome.Missing);
            read.Ok.Should().BeFalse();
        }

        [Theory]
        [InlineData(HttpStatusCode.TooManyRequests)]
        [InlineData(HttpStatusCode.InternalServerError)]
        [InlineData(HttpStatusCode.BadGateway)]
        public async Task ASourceProblem_IsUnavailable_AndNeverAnException(HttpStatusCode status)
        {
            var catalog = Catalog(new FakeHandler(status, ""));

            var read = await catalog.GetPriceAsync("004380-0", "2014-5", 337);

            read.Outcome.Should().Be(FipeOutcome.Unavailable);
            read.Detail.Should().NotBeNullOrWhiteSpace();
        }

        [Fact]
        public async Task TheSourceOutOfReach_IsUnavailable()
        {
            var catalog = Catalog(new FakeHandler(new HttpRequestException("sem rota para o host")));

            var read = await catalog.GetPriceAsync("004380-0", "2014-5", 337);

            read.Outcome.Should().Be(FipeOutcome.Unavailable);
        }

        [Fact]
        public async Task AShapeThisAdapterCannotRead_IsUnavailable_AndNeverAnException()
        {
            // O dia em que o espelho mudar de formato é o dia em que isto acontece, e ele não
            // pode derrubar quem estava só salvando um veículo.
            var catalog = Catalog(Answer("""{ "preco": "56530" }"""));

            var read = await catalog.GetPriceAsync("004380-0", "2014-5", 337);

            read.Ok.Should().BeFalse();
            read.Outcome.Should().Be(FipeOutcome.Unavailable);
        }

        [Fact]
        public async Task APriceTheAdapterCannotRead_IsUnavailable()
        {
            var catalog = Catalog(Answer("""
                { "price": "consulte", "referenceMonth": "setembro de 2026", "codeFipe": "004380-0" }
                """));

            var read = await catalog.GetPriceAsync("004380-0", "2014-5", 337);

            read.Outcome.Should().Be(FipeOutcome.Unavailable);
            read.Detail.Should().Contain("consulte");
        }

        [Fact]
        public async Task TheYearsOfAModel_CarryTheCodeAndTheYearApart()
        {
            var catalog = Catalog(Answer(YearsBody));

            var read = await catalog.ListYearsAsync("004380-0", 337);

            read.Ok.Should().BeTrue();
            read.Value.Should().HaveCount(3);

            // O ano sozinho é ambíguo: o mesmo carro e ano existem como flex e como gasolina,
            // com preços diferentes. O código do par é o que a tabela precifica.
            var year2014 = read.Value!.Single(option => option.ModelYear == 2014);
            year2014.YearFuel.Should().Be("2014-5");
            year2014.Name.Should().Be("2014 Flex");
        }

        [Fact]
        public async Task WithTheAutomaticQueryTurnedOff_NothingReachesTheNetwork()
        {
            var handler = Answer(PriceBody);
            var catalog = Catalog(handler, enabled: false);

            var read = await catalog.GetPriceAsync("004380-0", "2014-5", 337);

            read.Outcome.Should().Be(FipeOutcome.Unavailable);
            handler.Calls.Should().Be(0);
        }

        [Theory]
        // "setembro de 2026" vem na resposta de preço e "setembro/2026" na lista de tabelas: a
        // mesma API escreve o mesmo mês de dois jeitos.
        [InlineData("setembro de 2026", 2026, 9)]
        [InlineData("setembro/2026", 2026, 9)]
        [InlineData("SETEMBRO DE 2026", 2026, 9)]
        // Março com e sem cedilha, porque as duas formas circulam por aí.
        [InlineData("março de 2026", 2026, 3)]
        [InlineData("marco/2026", 2026, 3)]
        [InlineData("janeiro de 2027", 2027, 1)]
        public async Task TheMonth_IsReadInEveryShapeTheSourceWrites(string text, int year, int month)
        {
            var catalog = Catalog(Answer($$"""
                {
                  "price": "R$ 56.530,00",
                  "codeFipe": "004380-0",
                  "modelYear": 2014,
                  "referenceMonth": "{{text}}"
                }
                """));

            var read = await catalog.GetPriceAsync("004380-0", "2014-5", 337);

            read.Ok.Should().BeTrue(text);
            read.Value!.Reference.Should().Be(new DateOnly(year, month, 1));
        }

        [Fact]
        public async Task AMonthTheAdapterCannotRead_IsUnavailable()
        {
            var catalog = Catalog(Answer("""
                { "price": "R$ 56.530,00", "codeFipe": "004380-0", "referenceMonth": "mês que vem" }
                """));

            var read = await catalog.GetPriceAsync("004380-0", "2014-5", 337);

            read.Outcome.Should().Be(FipeOutcome.Unavailable);
            read.Detail.Should().Contain("Mês ilegível");
        }

        private const string BrandsBody = """
            [
              { "code": "1", "name": "Acura" },
              { "code": "21", "name": "Fiat" },
              { "code": "23", "name": "GM - Chevrolet" }
            ]
            """;

        [Fact]
        public async Task TheBrands_ComeAsCodeAndName()
        {
            var handler = Answer(BrandsBody);
            var catalog = Catalog(handler);

            var read = await catalog.ListBrandsAsync();

            read.Ok.Should().BeTrue();
            read.Value.Should().HaveCount(3);
            read.Value!.Single(brand => brand.Code == "23").Name.Should().Be("GM - Chevrolet");
        }

        [Fact]
        public async Task TheListingCalls_StayUnpinned()
        {
            var handler = Answer(BrandsBody);
            var catalog = Catalog(handler);

            await catalog.ListBrandsAsync();
            handler.LastPath.Should().Be("https://fonte.exemplo/api/v2/cars/brands");

            await catalog.ListModelsAsync("23");
            handler.LastPath.Should().Be("https://fonte.exemplo/api/v2/cars/brands/23/models");

            await catalog.ListModelYearsAsync("23", "5635");

            // Uma lista de nomes é outra coisa de um preço: marca e modelo mal se movem entre
            // duas tabelas mensais, e fixar o mês custaria o dobro de chamadas num escolhedor
            // que a pessoa percorre em três passos. O preço que vem depois é fixado, e é ele
            // que corrige qualquer diferença — porque o código guardado é o que ele imprimiu.
            handler.LastPath.Should().Be(
                "https://fonte.exemplo/api/v2/cars/brands/23/models/5635/years");
        }

        [Fact]
        public async Task ThePriceOfAChosenModel_IsPinned_AndAnswersTheCodeOfTheModel()
        {
            var handler = Answer(PriceBody);
            var catalog = Catalog(handler);

            var read = await catalog.GetPriceOfModelAsync("23", "5635", "2014-5", 337);

            handler.LastPath.Should().Be(
                "https://fonte.exemplo/api/v2/cars/brands/23/models/5635/years/2014-5?reference=337");

            // É esta a única chamada que responde o código do modelo, e é por isso que ela
            // existe: sem ele, todo carro voltaria a ser procurado por marca e modelo.
            read.Ok.Should().BeTrue();
            read.Value!.FipeCode.Should().Be("004380-0");
            read.Value.Value.Should().Be(56_530.00m);
        }

        [Fact]
        public async Task AChosenModelAnsweredWithoutACode_IsUnavailable()
        {
            // Sem código, esta resposta serve para nada: ela existe justamente para aprendê-lo.
            var catalog = Catalog(Answer("""
                { "price": "R$ 56.530,00", "referenceMonth": "setembro de 2026" }
                """));

            var read = await catalog.GetPriceOfModelAsync("23", "5635", "2014-5", 337);

            read.Outcome.Should().Be(FipeOutcome.Unavailable);
            read.Detail.Should().Contain("código");
        }

        [Fact]
        public async Task ABrandWithNoModels_IsMissing()
        {
            var catalog = Catalog(Answer("[]"));

            var read = await catalog.ListModelsAsync("999");

            read.Outcome.Should().Be(FipeOutcome.Missing);
        }

        private static FipeHttpCatalog Catalog(
            FakeHandler handler,
            string token = "",
            bool enabled = true)
        {
            var client = new HttpClient(handler)
            {
                BaseAddress = new Uri("https://fonte.exemplo/api/v2/")
            };

            var settings = new FipeSettings
            {
                Enabled = enabled,
                BaseUrl = "https://fonte.exemplo/api/v2",
                VehicleType = "cars",
                Token = token,
                TimeoutInSeconds = 8
            };

            return new FipeHttpCatalog(
                client,
                Options.Create(settings),
                NullLogger<FipeHttpCatalog>.Instance);
        }

        private static FakeHandler Answer(string body) => new(HttpStatusCode.OK, body);

        /// <summary>A fonte, de mentira: responde o que o teste mandou e conta o que recebeu.</summary>
        private sealed class FakeHandler : HttpMessageHandler
        {
            private readonly HttpStatusCode status;
            private readonly string body = string.Empty;
            private readonly Exception? failure;

            public FakeHandler(HttpStatusCode status, string body)
            {
                this.status = status;
                this.body = body;
            }

            public FakeHandler(Exception failure) => this.failure = failure;

            public string? LastPath { get; private set; }

            public string? LastToken { get; private set; }

            public int Calls { get; private set; }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                Calls++;
                LastPath = request.RequestUri?.ToString();
                LastToken = request.Headers.TryGetValues("X-Subscription-Token", out var values)
                    ? values.FirstOrDefault()
                    : null;

                if (failure is not null)
                {
                    return Task.FromException<HttpResponseMessage>(failure);
                }

                return Task.FromResult(new HttpResponseMessage(status)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                });
            }
        }
    }
}
