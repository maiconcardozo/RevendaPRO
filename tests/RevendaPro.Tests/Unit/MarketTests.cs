using FluentAssertions;
using Moq;
using RevendaPro.Application.Market.DTOs;
using RevendaPro.Application.Market.Handlers;
using RevendaPro.Application.Market.Queries;
using RevendaPro.Domain.Enums;
using RevendaPro.Domain.Interfaces;
using RevendaPro.Domain.Interfaces.Repositories;
using RevendaPro.Domain.Interfaces.Security;
using RevendaPro.Domain.ValueObjects;

namespace RevendaPro.Tests.Unit
{
    /// <summary>
    /// A revenda contra a tabela, que é a pergunta que abriu o M11.
    ///
    /// <i>"Este carro foi vendido por R$ 60.000 quando a tabela do mês dizia R$ 56.530 — 6,1%
    /// acima."</i> A frase inteira depende de <b>qual mês</b>: comparar uma venda de agosto com
    /// a tabela de hoje mediria a passagem do tempo e chamaria isso de resultado. É por isso
    /// que a cotação de mês fechado jamais muda, e é o que os testes abaixo seguram.
    /// </summary>
    public class MarketTests
    {
        private static readonly DateOnly Setembro = new(2026, 9, 1);
        private static readonly DateOnly Agosto = new(2026, 8, 1);
        private static readonly DateOnly Julho = new(2026, 7, 1);

        [Fact]
        public async Task ASale_IsMeasuredAgainstTheTableOfItsOwnMonth()
        {
            var world = new World();

            // Vendido em agosto por R$ 60.000, com a tabela de agosto em R$ 56.815 — e a de
            // setembro em R$ 56.530. Medir pela de setembro daria 6,1% em vez de 5,6%, e o
            // erro seria a queda do mês, apresentada como resultado da negociação.
            world.GivenSold(closed: 60_000m, month: Agosto, tableThatMonth: 56_815m);

            var market = await world.Overview();

            market.Sales.Cars.Should().Be(1);
            market.Sales.Percent.Should().Be(5.61m);
            market.Sold[0].Reference.Should().Be(56_815m);
            market.Sold[0].Difference.Should().Be(3_185m);
        }

        [Fact]
        public async Task APurchase_IsMeasuredAgainstTheTableOfTheMonthItCameIn()
        {
            var world = new World();
            world.GivenOnTheLot(paid: 29_450m, purchasedIn: Julho, tableThatMonth: 57_101m);

            var market = await world.Overview();

            // A vantagem do leilão, medida em vez de suposta.
            market.Purchases.Percent.Should().Be(-48.42m);
            market.Yard[0].PurchasePercent.Should().Be(-48.42m);
        }

        [Fact]
        public async Task WhatTheYardLost_IsTheFallOfTheTable()
        {
            var world = new World();

            // O Cruze do levantamento: 57.101 em julho, 56.815 em agosto, 56.530 em setembro.
            world.GivenOnTheLot(
                paid: 29_450m, purchasedIn: Julho, tableThatMonth: 57_101m,
                previousTable: 56_815m, currentTable: 56_530m);

            var market = await world.Overview();

            // Uns R$ 285 por mês, que é o custo de segurar o carro — e o número que ninguém
            // conseguia dizer antes deste marco.
            market.LostThisMonth.Should().Be(285m);
            market.LostSincePurchase.Should().Be(571m);
            market.Yard[0].LostSincePurchase.Should().Be(571m);
        }

        [Fact]
        public async Task ADealWithoutTheQuoteOfItsMonth_SaysSoInsteadOfInventing()
        {
            var world = new World();

            // O sistema só passou a guardar cotações no M11, e a faixa gratuita devolve três
            // meses. Venda anterior a isso fica sem comparação, e a tela diz isso.
            world.GivenSold(closed: 60_000m, month: Agosto, tableThatMonth: null);

            var market = await world.Overview();

            market.Sold[0].Reference.Should().BeNull();
            market.Sold[0].Percent.Should().BeNull();

            // E ela fica fora da média: uma média tirada sobre metade dos carros, apresentada
            // como a revenda inteira, é uma mentira com aparência de número.
            market.Sales.Cars.Should().Be(0);
        }

        [Fact]
        public async Task TheAverageWeighsMoney_AndNeverPercentages()
        {
            var world = new World();

            world.GivenSold(closed: 100_000m, month: Setembro, tableThatMonth: 100_000m);
            world.GivenSold(closed: 12_000m, month: Setembro, tableThatMonth: 10_000m);

            var market = await world.Overview();

            // Somar os valores e dividir uma vez no fim: pela média dos percentuais daria 10%,
            // e o carro de dez mil teria o mesmo peso do de cem mil numa pergunta sobre
            // dinheiro.
            market.Sales.Amount.Should().Be(112_000m);
            market.Sales.Reference.Should().Be(110_000m);
            market.Sales.Percent.Should().Be(1.82m);
        }

        [Fact]
        public async Task CarsWithNoQuoteOfThisMonth_AreCounted()
        {
            var world = new World();
            world.GivenOnTheLot(paid: 20_000m, purchasedIn: Setembro, tableThatMonth: null);

            var market = await world.Overview();

            market.WithoutReference.Should().Be(1);
        }

        [Fact]
        public async Task AnOfferOnTheTable_IsMeasuredAgainstTheTableOfNow()
        {
            var world = new World();
            world.GivenOffer("Cliente do anúncio", 54_000m, currentTable: 56_530m);

            var market = await world.Overview();

            market.Proposals.Should().ContainSingle();
            market.Proposals[0].Difference.Should().Be(-2_530m);
            market.Proposals[0].Percent.Should().Be(-4.48m);
        }

        private sealed class World
        {
            private readonly List<MarketPosition> positions = [];
            private readonly List<MarketProposal> proposals = [];

            public World()
            {
                var vehicles = new Mock<IVehicleRepository>();

                vehicles.Setup(repository => repository.ListMarketPositionsAsync(
                        It.IsAny<int>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(),
                        It.IsAny<CancellationToken>()))
                    .ReturnsAsync(() => positions);

                vehicles.Setup(repository => repository.ListMarketProposalsAsync(
                        It.IsAny<int>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(() => proposals);

                var unitOfWork = new Mock<IUnitOfWork>();
                unitOfWork.SetupGet(unit => unit.VehicleRepository).Returns(vehicles.Object);

                var currentUser = new Mock<ICurrentUser>();
                currentUser.SetupGet(user => user.IdTenant).Returns(7);

                Handler = new GetMarketOverviewHandler(unitOfWork.Object, currentUser.Object);
            }

            private GetMarketOverviewHandler Handler { get; }

            public Task<MarketOverviewDto> Overview() =>
                Handler.Handle(new GetMarketOverviewQuery(), CancellationToken.None);

            public void GivenSold(decimal closed, DateOnly month, decimal? tableThatMonth) =>
                positions.Add(new MarketPosition(
                    Guid.CreateVersion7(), "MKT7H21", "Chevrolet", "Cruze", null, 2014,
                    VehicleStatus.Sold, 61, 29_450m, Julho, null,
                    null, 56_530m, 56_815m, closed, month, tableThatMonth));

            public void GivenOnTheLot(
                decimal paid,
                DateOnly purchasedIn,
                decimal? tableThatMonth,
                decimal? previousTable = null,
                decimal? currentTable = null) =>
                positions.Add(new MarketPosition(
                    Guid.CreateVersion7(), "ABC1D23", "Chevrolet", "Cruze", null, 2014,
                    VehicleStatus.ReadyForSale, 63, paid, purchasedIn, tableThatMonth,
                    null, currentTable, previousTable, null, null, null));

            public void GivenOffer(string who, decimal amount, decimal? currentTable) =>
                proposals.Add(new MarketProposal(
                    Guid.CreateVersion7(), "MKT7H21", "Chevrolet", "Cruze",
                    who, amount, Setembro, currentTable));
        }
    }
}
