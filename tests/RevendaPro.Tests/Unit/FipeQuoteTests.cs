using System.Reflection;
using FluentAssertions;
using Moq;
using RevendaPro.Application.Fipe;
using RevendaPro.Domain.Entities;
using RevendaPro.Domain.Interfaces;
using RevendaPro.Domain.Interfaces.Reference;
using RevendaPro.Domain.Interfaces.Repositories;
using RevendaPro.Domain.ValueObjects;

namespace RevendaPro.Tests.Unit
{
    /// <summary>
    /// A cotação guardada, que é o que torna a consulta automática barata.
    ///
    /// A tabela muda uma vez por mês e o pátio tem dezenas de carros. Sem guardar, cada ficha
    /// aberta seria uma chamada à fonte, e a faixa gratuita acabaria à toa. O que se prova
    /// aqui é a promessa do V2: dois carros do mesmo modelo e ano custam <b>uma</b> consulta,
    /// e um mês já buscado jamais volta à rede.
    ///
    /// Junto vem a garantia que sustenta a comparação histórica do M11: cotação de mês
    /// fechado é fato, e o sistema fica sem como mudar uma.
    /// </summary>
    public class FipeQuoteTests
    {
        private static readonly DateOnly Setembro = new(2026, 9, 1);
        private static readonly DateOnly Agosto = new(2026, 8, 1);

        [Fact]
        public async Task TwoCarsOfTheSameModelAndYear_CostOneQuery()
        {
            var world = new World();

            var first = await world.Read("004380-0", "2014-5");
            var second = await world.Read("004380-0", "2014-5");

            first.Ok.Should().BeTrue();
            second.Value.Should().BeSameAs(first.Value);

            // A conta inteira do marco está nestas três verificações: a tabela publicada é
            // resolvida uma vez, o preço é pedido uma vez, e a cotação é escrita uma vez.
            world.Catalog.Verify(
                catalog => catalog.GetCurrentReferenceAsync(It.IsAny<CancellationToken>()),
                Times.Once);

            world.Catalog.Verify(
                catalog => catalog.GetPriceAsync(
                    "004380-0", "2014-5", 337, It.IsAny<CancellationToken>()),
                Times.Once);

            world.Quotes.Verify(
                repository => repository.Add(It.IsAny<FipeQuote>()), Times.Once);
        }

        [Fact]
        public async Task AMonthAlreadyKept_NeverGoesBackToTheNetwork()
        {
            var world = new World();
            world.GivenKept("004380-0", "2014-5", Setembro, 56_530.00m);

            var read = await world.Read("004380-0", "2014-5");

            read.Ok.Should().BeTrue();
            read.Value!.Value.Should().Be(56_530.00m);

            // O preço de um mês fechado já é conhecido: perguntar de novo gastaria uma
            // consulta para receber exatamente o mesmo número.
            world.Catalog.Verify(
                catalog => catalog.GetPriceAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);

            world.Quotes.Verify(repository => repository.Add(It.IsAny<FipeQuote>()), Times.Never);
        }

        [Fact]
        public async Task AnotherYearFuel_CostsItsOwnQuery()
        {
            var world = new World();

            await world.Read("004380-0", "2014-5");
            await world.Read("004380-0", "2016-5");

            // Guarda do próprio guardar: o par é o que a tabela precifica, então o mesmo
            // código com outro ano-combustível é outro preço, e jamais o mesmo de novo.
            world.Catalog.Verify(
                catalog => catalog.GetPriceAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(),
                    It.IsAny<CancellationToken>()),
                Times.Exactly(2));
        }

        [Fact]
        public async Task TheMonthKept_IsTheOneTheAnswerCarried()
        {
            var world = new World();
            world.TheSourceAnswersTheMonthOf(Agosto);

            var read = await world.Read("004380-0", "2014-5");

            // A fonte se contradisse no levantamento: pedindo a tabela de setembro, veio
            // agosto. O que fica guardado é o mês da resposta, porque é a ele que o valor
            // pertence — carimbar setembro criaria um fato histórico falso.
            read.Value!.ReferenceMonth.Should().Be(Agosto);
        }

        [Fact]
        public async Task WhenTheAnsweredMonthIsAlreadyKept_NothingIsWrittenTwice()
        {
            var world = new World();
            world.TheSourceAnswersTheMonthOf(Agosto);
            world.GivenKept("004380-0", "2014-5", Agosto, 56_815.00m);

            var read = await world.Read("004380-0", "2014-5");

            read.Value!.Value.Should().Be(56_815.00m);

            // Escrever de novo o mesmo mês esbarraria no índice único, e é justamente ele que
            // garante a leitura de uma linha só.
            world.Quotes.Verify(repository => repository.Add(It.IsAny<FipeQuote>()), Times.Never);
        }

        [Fact]
        public async Task WithTheSourceOutOfReach_TheReadingSaysSo_AndNothingIsKept()
        {
            var world = new World();
            world.TheSourceIsOutOfReach();

            var read = await world.Read("004380-0", "2014-5");

            read.Outcome.Should().Be(FipeOutcome.Unavailable);
            read.Ok.Should().BeFalse();

            world.Quotes.Verify(repository => repository.Add(It.IsAny<FipeQuote>()), Times.Never);
        }

        [Fact]
        public async Task WithoutTheListOfTables_NoPriceIsEvenAsked()
        {
            var world = new World();
            world.TheListOfTablesIsOutOfReach();

            var read = await world.Read("004380-0", "2014-5");

            read.Outcome.Should().Be(FipeOutcome.Unavailable);

            // Sem saber qual tabela está publicada, perguntar o preço devolveria "a atual" —
            // que foi exatamente o que fez a mesma fonte responder dois meses diferentes.
            world.Catalog.Verify(
                catalog => catalog.GetPriceAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task ACarOutsideTheTable_IsMissing_AndStaysAFinalFact()
        {
            var world = new World();
            world.TheCarIsOutsideTheTable();

            var read = await world.Read("999999-9", "2014-5");

            // Importado, muito antigo ou nunca precificado: a resposta é definitiva, e o
            // chamador precisa distinguir isso de "a fonte caiu".
            read.Outcome.Should().Be(FipeOutcome.Missing);

            world.Quotes.Verify(repository => repository.Add(It.IsAny<FipeQuote>()), Times.Never);
        }

        [Fact]
        public void AQuoteKeepsTheValueOfTheTable_InDecimal()
        {
            var quote = FipeQuote.Create(new FipePrice(
                "004380-0", "2014-5", new DateOnly(2026, 9, 30), 56_530.00m,
                "GM - Chevrolet", "CRUZE LT 1.8 16V FlexPower 4p Aut.", 2014, "Flex"));

            quote.Value.Should().Be(56_530.00m);
            quote.Model.Should().Be("CRUZE LT 1.8 16V FlexPower 4p Aut.");

            // Sempre o dia primeiro: a tabela é mensal, então o dia carrega zero significado e
            // duas leituras do mesmo mês precisam cair na mesma data para se comparar.
            quote.ReferenceMonth.Should().Be(Setembro);
        }

        [Fact]
        public void AQuoteOfAClosedMonth_HasNoWayToChange()
        {
            var writable = typeof(FipeQuote)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(method => !method.IsSpecialName)
                .Select(method => method.Name)
                .ToList();

            // O que sustenta a comparação histórica do marco inteiro: "vendido por R$ 60.000
            // quando a tabela do mês dizia R$ 56.815" só continua verdade se o valor daquele
            // mês jamais for reescrito. Fábrica, e nenhum método de instância.
            writable.Should().BeEmpty();
        }

        [Fact]
        public void AValueOfZero_IsRefused()
        {
            var act = () => FipeQuote.Create("004380-0", "2014-5", Setembro, 0m, 2014, "GM", "Cruze");

            // Ninguém digita cotação: um zero aqui é o adaptador tendo deixado passar algo que
            // ele mesmo devia ter recusado.
            act.Should().Throw<ArgumentOutOfRangeException>();
        }

        /// <summary>A fonte e a tabela guardada, ambas de mentira.</summary>
        private sealed class World
        {
            private readonly List<FipeQuote> kept = [];
            private FipeResult<FipePrice> answer;
            private FipeResult<FipeReference> tables;

            public World()
            {
                tables = FipeResult<FipeReference>.Found(new FipeReference(337, Setembro));
                answer = Price(Setembro, 56_530.00m);

                Quotes = new Mock<IFipeQuoteRepository>();

                Quotes.Setup(repository => repository.FindAsync(
                        It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly>(),
                        It.IsAny<CancellationToken>()))
                    .ReturnsAsync((string code, string yearFuel, DateOnly month, CancellationToken _) =>
                        kept.FirstOrDefault(quote =>
                            quote.FipeCode == code
                            && quote.YearFuel == yearFuel
                            && quote.ReferenceMonth == month));

                Catalog = new Mock<IFipeCatalog>();

                Catalog.Setup(catalog => catalog.GetCurrentReferenceAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(() => tables);

                Catalog.Setup(catalog => catalog.GetPriceAsync(
                        It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(),
                        It.IsAny<CancellationToken>()))
                    .ReturnsAsync(() => answer);

                var unitOfWork = new Mock<IUnitOfWork>();
                unitOfWork.SetupGet(unit => unit.FipeQuoteRepository).Returns(Quotes.Object);

                Reader = new FipeQuoteReader(unitOfWork.Object, Catalog.Object);
            }

            public Mock<IFipeCatalog> Catalog { get; }

            public Mock<IFipeQuoteRepository> Quotes { get; }

            private FipeQuoteReader Reader { get; }

            public Task<FipeResult<FipeQuote>> Read(string fipeCode, string yearFuel) =>
                Reader.GetCurrentAsync(fipeCode, yearFuel);

            public void GivenKept(string fipeCode, string yearFuel, DateOnly month, decimal value) =>
                kept.Add(FipeQuote.Create(fipeCode, yearFuel, month, value, 2014, "GM", "Cruze"));

            public void TheSourceAnswersTheMonthOf(DateOnly month) =>
                answer = Price(month, 56_815.00m);

            public void TheSourceIsOutOfReach() =>
                answer = FipeResult<FipePrice>.Unavailable("A fonte está fora de alcance.");

            public void TheListOfTablesIsOutOfReach() =>
                tables = FipeResult<FipeReference>.Unavailable("A fonte está fora de alcance.");

            public void TheCarIsOutsideTheTable() =>
                answer = FipeResult<FipePrice>.Missing("A tabela respondeu 404.");

            private static FipeResult<FipePrice> Price(DateOnly month, decimal value) =>
                FipeResult<FipePrice>.Found(new FipePrice(
                    "004380-0", "2014-5", month, value,
                    "GM - Chevrolet", "CRUZE LT 1.8 16V FlexPower 4p Aut.", 2014, "Flex"));
        }
    }
}
