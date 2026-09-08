using FluentAssertions;
using Moq;
using RevendaPro.Application.Suppliers.Handlers;
using RevendaPro.Application.Suppliers.Queries;
using RevendaPro.Domain.Entities;
using RevendaPro.Domain.Interfaces;
using RevendaPro.Domain.Interfaces.Repositories;
using RevendaPro.Domain.Interfaces.Security;
using RevendaPro.Shared.Exceptions;

namespace RevendaPro.Tests.Unit
{
    /// <summary>
    /// Quanto já foi para cada fornecedor.
    ///
    /// <i>"No dash preciso de um painel onde eu vou ver quais são os fornecedores que eu mais
    /// gastei, e ter um espaço exclusivo também para ver essa questão do gasto."</i>
    ///
    /// A soma é do banco (decisão 5 do M18); o que se prova aqui é o que a aplicação faz com
    /// ela: o ranking vem do maior para o menor pelo que foi <b>pago</b>, quem não recebeu nada
    /// fica de fora, e a ficha separa pago de previsto e quebra por tipo.
    /// </summary>
    public class DashboardSupplierTests
    {
        private const int IdTenant = 7;

        [Fact]
        public async Task TheRanking_ComesFromTheDatabaseSum_WithNameAndSegment()
        {
            var world = new World();
            var silva = world.GivenSupplier(1, "Auto Mecânica Silva");
            var ze = world.GivenSupplier(2, "Funilaria do Zé");
            world.TheDatabaseSums(
                new SupplierSpend(ze.Id, 4_000m, 0m, 2, new DateOnly(2026, 9, 1)),
                new SupplierSpend(silva.Id, 2_150m, 800m, 3, new DateOnly(2026, 8, 20)));

            var ranking = await world.Spending();

            ranking.Should().HaveCount(2);
            ranking[0].Name.Should().Be("Funilaria do Zé");
            ranking[0].SegmentName.Should().Be("Oficina mecânica");
            ranking[0].PaidTotal.Should().Be(4_000m);
            ranking[1].PlannedTotal.Should().Be(800m);
            ranking[1].ExpenseCount.Should().Be(3);
        }

        [Fact]
        public async Task ASupplierNobodyPaid_StaysOutOfTheRanking()
        {
            var world = new World();
            world.GivenSupplier(1, "Auto Mecânica Silva");
            world.GivenSupplier(2, "Guincho 24h");
            world.TheDatabaseSums(new SupplierSpend(1, 500m, 0m, 1, null));

            var ranking = await world.Spending();

            ranking.Should().ContainSingle().Which.Name.Should().Be("Auto Mecânica Silva");
        }

        [Fact]
        public async Task WithNothingSpent_TheRankingIsEmpty_AndNobodyIsAskedForNames()
        {
            var world = new World();
            world.GivenSupplier(1, "Auto Mecânica Silva");

            var ranking = await world.Spending();

            ranking.Should().BeEmpty();
            world.Suppliers.Verify(
                repository => repository.ListByTenantAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task TheStatistics_NameEachSlice_AndFillTheEmptyMonths()
        {
            var world = new World();
            world.GivenSupplier(1, "Auto Mecânica Silva");
            world.TheDatabaseSums(new SupplierSpend(1, 3_000m, 0m, 2, null));
            world.TheDatabaseStatistics(new SupplierStatistics(
                3_000m, 500m, 2, 1, 120m,
                [new SpendSlice(1, 3_000m, 500m, 2)],
                [new SpendSlice(World.Mechanics, 2_000m, 0m, 1), new SpendSlice(World.Parts, 1_000m, 500m, 1)],
                [new SpendSlice(202607, 1_000m, 0m, 1), new SpendSlice(202609, 2_000m, 500m, 1)]));

            var statistics = await world.Statistics(new DateOnly(2026, 6, 1), new DateOnly(2026, 9, 30));

            statistics.PaidTotal.Should().Be(3_000m);
            statistics.UnassignedPaid.Should().Be(120m);
            statistics.SupplierCount.Should().Be(1);
            statistics.BySegment.Single().Name.Should().Be("Oficina mecânica");
            statistics.ByType.Select(slice => slice.Name).Should().ContainInOrder("Mecânica", "Peças");

            // Quatro meses pedidos, quatro colunas: junho e agosto entram zerados, porque um
            // gráfico com buraco lê como erro, e não como mês sem gasto.
            statistics.ByMonth.Select(month => month.Key).Should().Equal("2026-06", "2026-07", "2026-08", "2026-09");
            statistics.ByMonth[0].PaidTotal.Should().Be(0m);
            statistics.ByMonth[1].PaidTotal.Should().Be(1_000m);
            statistics.ByMonth[3].PlannedTotal.Should().Be(500m);
        }

        [Fact]
        public async Task WithAnOpenPeriod_TheMonthlySeries_CoversTheLastTwelveMonths()
        {
            var world = new World();
            world.TheDatabaseStatistics(new SupplierStatistics(0m, 0m, 0, 0, 0m, [], [], []));

            var statistics = await world.Statistics(null, null);

            statistics.ByMonth.Should().HaveCount(12);
            statistics.ByMonth[^1].Key.Should().Be(DateTime.UtcNow.ToString("yyyy-MM"));
        }

        [Fact]
        public async Task TheStatement_SeparatesPaidFromPlanned_AndBreaksDownByType()
        {
            var world = new World();
            var silva = world.GivenSupplier(1, "Auto Mecânica Silva");
            world.TheSupplierHasLines(
                new SupplierExpenseLine(Guid.NewGuid(), new DateOnly(2026, 9, 1), "Troca de embreagem", 2_400m, true,
                    World.Mechanics, Guid.NewGuid(), "ABC1D23", "Honda", "Civic", "2.0 EXL", 2019),
                new SupplierExpenseLine(Guid.NewGuid(), new DateOnly(2026, 8, 20), "Kit de embreagem", 1_450m, true,
                    World.Parts, Guid.NewGuid(), "DEF4G56", "Chevrolet", "Onix", null, 2020),
                new SupplierExpenseLine(Guid.NewGuid(), new DateOnly(2026, 8, 10), "Revisão dos freios", 900m, false,
                    World.Mechanics, Guid.NewGuid(), "ABC1D23", "Honda", "Civic", "2.0 EXL", 2019));

            var statement = await world.Statement(silva.Code);

            statement.PaidTotal.Should().Be(3_850m);
            statement.PlannedTotal.Should().Be(900m);
            statement.Supplier.ExpenseCount.Should().Be(3);

            // Mecânica vem primeiro porque foi onde mais se pagou; o previsto conta na quantidade
            // e jamais no total pago.
            statement.ByType.Should().HaveCount(2);
            statement.ByType[0].ExpenseTypeName.Should().Be("Mecânica");
            statement.ByType[0].PaidTotal.Should().Be(2_400m);
            statement.ByType[0].ExpenseCount.Should().Be(2);

            statement.Expenses[0].VehicleName.Should().Be("Honda Civic 2.0 EXL 2019");
            statement.Expenses[1].VehicleName.Should().Be("Chevrolet Onix 2020");
        }

        [Fact]
        public async Task TheStatementOfAnUnknownSupplier_IsRefused()
        {
            var world = new World();

            var act = () => world.Statement(Guid.NewGuid());

            await act.Should().ThrowAsync<NotFoundException>().WithMessage("*Fornecedor inexistente*");
        }

        private sealed class World
        {
            public const int Mechanics = 10;
            public const int Parts = 11;

            private readonly List<Supplier> suppliers = [];
            private IReadOnlyList<SupplierSpend> sums = [];
            private IReadOnlyList<SupplierExpenseLine> lines = [];
            private SupplierStatistics statistics = new(0m, 0m, 0, 0, 0m, [], [], []);

            public World()
            {
                var workshop = SupplierSegment.Create(IdTenant, "Oficina mecânica");
                workshop.Id = 1;

                var segments = new Mock<ISupplierSegmentRepository>();
                segments.Setup(repository => repository.ListByTenantAsync(IdTenant, It.IsAny<CancellationToken>()))
                    .ReturnsAsync([workshop]);

                Suppliers = new Mock<ISupplierRepository>();
                Suppliers.Setup(repository => repository.ListByTenantAsync(IdTenant, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(() => suppliers);
                Suppliers.Setup(repository => repository.GetByCodeAsync(
                        IdTenant, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((int _, Guid code, CancellationToken _) =>
                        suppliers.FirstOrDefault(supplier => supplier.Code == code));
                Suppliers.Setup(repository => repository.SumByTenantAsync(
                        IdTenant, It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(() => sums);
                Suppliers.Setup(repository => repository.ReadStatisticsAsync(
                        IdTenant, It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(() => statistics);
                Suppliers.Setup(repository => repository.ListExpensesAsync(
                        IdTenant, It.IsAny<int>(), It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(),
                        It.IsAny<CancellationToken>()))
                    .ReturnsAsync(() => lines);

                var mechanics = ExpenseType.Create(IdTenant, "Mecânica");
                mechanics.Id = Mechanics;
                var parts = ExpenseType.Create(IdTenant, "Peças");
                parts.Id = Parts;

                var types = new Mock<IExpenseTypeRepository>();
                types.Setup(repository => repository.ListByTenantAsync(IdTenant, It.IsAny<CancellationToken>()))
                    .ReturnsAsync([mechanics, parts]);

                var unitOfWork = new Mock<IUnitOfWork>();
                unitOfWork.SetupGet(unit => unit.SupplierRepository).Returns(Suppliers.Object);
                unitOfWork.SetupGet(unit => unit.SupplierSegmentRepository).Returns(segments.Object);
                unitOfWork.SetupGet(unit => unit.ExpenseTypeRepository).Returns(types.Object);

                var currentUser = new Mock<ICurrentUser>();
                currentUser.SetupGet(user => user.IdTenant).Returns(IdTenant);

                SpendingHandler = new ListSupplierSpendingHandler(unitOfWork.Object, currentUser.Object);
                StatementHandler = new GetSupplierStatementHandler(unitOfWork.Object, currentUser.Object);
                StatisticsHandler = new GetSupplierStatisticsHandler(unitOfWork.Object, currentUser.Object);
            }

            public Mock<ISupplierRepository> Suppliers { get; }

            private ListSupplierSpendingHandler SpendingHandler { get; }

            private GetSupplierStatementHandler StatementHandler { get; }

            private GetSupplierStatisticsHandler StatisticsHandler { get; }

            public Supplier GivenSupplier(int id, string name)
            {
                var supplier = Supplier.Create(IdTenant, name, idSupplierSegment: 1);
                supplier.Id = id;
                suppliers.Add(supplier);

                return supplier;
            }

            public void TheDatabaseSums(params SupplierSpend[] rows) => sums = rows;

            public void TheSupplierHasLines(params SupplierExpenseLine[] rows) => lines = rows;

            public void TheDatabaseStatistics(SupplierStatistics value) => statistics = value;

            public Task<Application.Suppliers.DTOs.SupplierStatisticsDto> Statistics(DateOnly? from, DateOnly? to) =>
                StatisticsHandler.Handle(new GetSupplierStatisticsQuery(from, to), CancellationToken.None);

            public Task<IReadOnlyList<Application.Suppliers.DTOs.SupplierSpendDto>> Spending() =>
                SpendingHandler.Handle(new ListSupplierSpendingQuery(null, null), CancellationToken.None);

            public Task<Application.Suppliers.DTOs.SupplierStatementDto> Statement(Guid code) =>
                StatementHandler.Handle(new GetSupplierStatementQuery(code, null, null), CancellationToken.None);
        }
    }
}
