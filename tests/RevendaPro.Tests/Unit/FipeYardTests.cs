using FluentAssertions;
using Moq;
using RevendaPro.Application.Fipe;
using RevendaPro.Domain.Entities;
using RevendaPro.Domain.Enums;
using RevendaPro.Domain.Interfaces;
using RevendaPro.Domain.Interfaces.Repositories;
using RevendaPro.Domain.ValueObjects;

namespace RevendaPro.Tests.Unit
{
    /// <summary>
    /// O pátio se atualizando sozinho, uma vez por mês.
    ///
    /// A conta que o stakeholder ainda não conseguia fazer: o Cruze do levantamento perde cerca
    /// de <b>R$ 285 por mês</b> de tabela parado no pátio. Sem esta rotina, o número da ficha
    /// envelhece em silêncio e a comparação do M11 mede o mês errado.
    ///
    /// O que se prova aqui é o critério do V5: uma rodada atualiza o pátio inteiro sem ninguém
    /// pedir, dez carros do mesmo modelo custam <b>uma</b> consulta, e o valor digitado à mão
    /// fica onde está.
    /// </summary>
    public class FipeYardTests
    {
        private static readonly DateOnly Setembro = new(2026, 9, 1);
        private static readonly DateOnly Agosto = new(2026, 8, 1);

        [Fact]
        public async Task OneRun_UpdatesTheWholeYard()
        {
            var world = new World();
            var um = world.GivenCarBehind("MKT7H21", "004380-0", "2014-5", 56_815m, Agosto);
            var outro = world.GivenCarBehind("ABC1D23", "001494-0", "2020-5", 52_000m, Agosto);

            var run = await world.Refresh();

            run.PublishedMonth.Should().Be(Setembro);
            run.Looked.Should().Be(2);
            run.Updated.Should().Be(2);

            um.FipeReferenceDate.Should().Be(Setembro);
            um.FipeValue.Should().Be(56_530m);
            outro.FipeReferenceDate.Should().Be(Setembro);

            world.UnitOfWork.Verify(
                unit => unit.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task TenCarsOfTheSameModel_CostOneQuery()
        {
            var world = new World();

            for (var i = 0; i < 10; i++)
            {
                world.GivenCarBehind($"CAR{i}A11", "004380-0", "2014-5", 56_815m, Agosto);
            }

            var run = await world.Refresh();

            run.Updated.Should().Be(10);

            // A conta inteira do marco: o pátio de dez Cruzes gasta uma consulta, e não dez.
            // É a mesma promessa do V2, agora medida numa rodada de verdade.
            run.Queries.Should().Be(1);
        }

        [Fact]
        public async Task ATypedValue_IsLeftAlone()
        {
            var world = new World();
            var raro = world.GivenCarBehind("RAR0A11", "004380-0", "2014-5", 56_815m, Agosto);

            // Digitado por quem conhece o mercado de um carro raro, importado ou fora da
            // tabela. A rotina substituiria esse julgamento por um número que ela nunca teve.
            // O valor precisa mudar de verdade: gravar o mesmo número mantém a origem, que é
            // a regra do V3 — o formulário devolve os campos como estão a cada gravação.
            raro.SetFipe(120_000m, Agosto, "004380-0");

            var run = await world.Refresh();

            run.LeftAlone.Should().Be(1);
            run.Updated.Should().Be(0);

            raro.FipeValue.Should().Be(120_000m);
            raro.FipeSource.Should().Be(FipeSource.Manual);
        }

        [Fact]
        public async Task ACarOutsideTheTable_IsCountedAndTheRunGoesOn()
        {
            var world = new World();
            world.GivenCarBehind("IMP0A11", "999999-9", "2014-5", 80_000m, Agosto);
            var normal = world.GivenCarBehind("MKT7H21", "004380-0", "2014-5", 56_815m, Agosto);

            world.TheTableHasNo("999999-9");

            var run = await world.Refresh();

            run.OutsideTheTable.Should().Be(1);

            // Um carro fora da tabela é fato final, e jamais motivo para o resto do pátio ficar
            // sem atualizar.
            run.Updated.Should().Be(1);
            normal.FipeReferenceDate.Should().Be(Setembro);
        }

        [Fact]
        public async Task WithTheTableOutOfReach_TheRunEndsWithoutTouchingAnything()
        {
            var world = new World();
            var carro = world.GivenCarBehind("MKT7H21", "004380-0", "2014-5", 56_815m, Agosto);

            world.TheTableIsOutOfReach();

            var run = await world.Refresh();

            run.PublishedMonth.Should().BeNull();
            run.Updated.Should().Be(0);

            // Toda ficha mantém o valor que tinha, marcado como velho, e a próxima rodada
            // tenta de novo.
            carro.FipeValue.Should().Be(56_815m);

            world.UnitOfWork.Verify(
                unit => unit.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ASourceThatStopsHalfway_KeepsWhatWasAlreadyWritten()
        {
            var world = new World();
            var primeiro = world.GivenCarBehind("MKT7H21", "004380-0", "2014-5", 56_815m, Agosto);
            var segundo = world.GivenCarBehind("ABC1D23", "001494-0", "2020-5", 52_000m, Agosto);

            world.TheSourceFallsAfter("004380-0");

            var run = await world.Refresh();

            run.Updated.Should().Be(1);
            primeiro.FipeReferenceDate.Should().Be(Setembro);

            // O resto espera a próxima rodada, em vez de martelar uma fonte que já está
            // sofrendo.
            segundo.FipeReferenceDate.Should().Be(Agosto);

            world.UnitOfWork.Verify(
                unit => unit.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public void AReferenceOfTwoMonthsAgo_IsTwoMonthsBehind()
        {
            var vehicle = Vehicle.Create(1, "MKT7H21", "9BWZZZ377VT004251", "Chevrolet", "Cruze", 2014, 2014);

            // Sem valor de referência, "velho" seria mentira: são coisas diferentes, e a tela
            // escreve as duas de jeitos diferentes.
            vehicle.FipeMonthsBehind(new DateOnly(2026, 9, 20)).Should().BeNull();

            vehicle.SetFipe(57_101m, new DateOnly(2026, 7, 1), "004380-0");

            vehicle.FipeMonthsBehind(new DateOnly(2026, 9, 20)).Should().Be(2);
            vehicle.FipeMonthsBehind(new DateOnly(2026, 7, 31)).Should().Be(0);

            // Mês digitado à frente de hoje continua sendo zero: ele tampouco é mais atual do
            // que o deste mês.
            vehicle.FipeMonthsBehind(new DateOnly(2026, 5, 1)).Should().Be(0);
        }

        private sealed class World
        {
            private readonly List<Vehicle> yard = [];
            private readonly HashSet<string> outsideTheTable = new(StringComparer.OrdinalIgnoreCase);
            private readonly HashSet<string> asked = new(StringComparer.OrdinalIgnoreCase);
            private readonly HashSet<string> codes = new(StringComparer.OrdinalIgnoreCase);
            private FipeResult<FipeReference> tables;
            private string? fallsAfter;
            private int queries;

            public World()
            {
                tables = FipeResult<FipeReference>.Found(new FipeReference(337, Setembro));

                Vehicles = new Mock<IVehicleRepository>();

                Vehicles.Setup(repository => repository.ListBehindFipeAsync(
                        It.IsAny<DateOnly>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((DateOnly month, int _, CancellationToken _) =>
                        yard.Where(vehicle =>
                                vehicle.FipeReferenceDate is null
                                || vehicle.FipeReferenceDate < month)
                            .ToList());

                UnitOfWork = new Mock<IUnitOfWork>();
                UnitOfWork.SetupGet(unit => unit.VehicleRepository).Returns(Vehicles.Object);

                Quotes = new Mock<IFipeQuoteReader>();

                Quotes.Setup(reader => reader.PublishedTableAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(() => tables);

                Quotes.SetupGet(reader => reader.Queries).Returns(() => queries);

                // O leitor de verdade guarda o que já buscou, então o mesmo modelo pedido dez
                // vezes é uma consulta só. Aqui isso é imitado contando pares distintos.
                Quotes.Setup(reader => reader.GetCurrentAsync(
                        It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((string code, string yearFuel, CancellationToken _) =>
                    {
                        if (outsideTheTable.Contains(code))
                        {
                            return FipeResult<FipeQuote>.Missing("A tabela respondeu 404.");
                        }

                        if (fallsAfter is not null && codes.Contains(fallsAfter) && code != fallsAfter)
                        {
                            return FipeResult<FipeQuote>.Unavailable("A fonte caiu.");
                        }

                        codes.Add(code);

                        if (asked.Add($"{code}|{yearFuel}"))
                        {
                            queries++;
                        }

                        return FipeResult<FipeQuote>.Found(FipeQuote.Create(
                            code, yearFuel, Setembro, ValueOf(code), 2014, "Marca", "Modelo"));
                    });

                Refresher = new FipeYardRefresher(UnitOfWork.Object, Quotes.Object);
            }

            public Mock<IUnitOfWork> UnitOfWork { get; }

            public Mock<IVehicleRepository> Vehicles { get; }

            public Mock<IFipeQuoteReader> Quotes { get; }

            private FipeYardRefresher Refresher { get; }

            public Vehicle GivenCarBehind(
                string plate, string fipeCode, string yearFuel, decimal value, DateOnly month)
            {
                var vehicle = Vehicle.Create(
                    1, plate, "9BWZZZ377VT004251", "Chevrolet", "Cruze", 2014, 2013);

                vehicle.Id = yard.Count + 1;
                vehicle.ApplyFipeReference(value, month, fipeCode, yearFuel);

                yard.Add(vehicle);

                return vehicle;
            }

            public Task<FipeYardRun> Refresh() => Refresher.RefreshAsync(CancellationToken.None);

            public void TheTableIsOutOfReach() =>
                tables = FipeResult<FipeReference>.Unavailable("A fonte está fora de alcance.");

            public void TheTableHasNo(string fipeCode) => outsideTheTable.Add(fipeCode);

            public void TheSourceFallsAfter(string fipeCode) => fallsAfter = fipeCode;

            private static decimal ValueOf(string fipeCode) =>
                fipeCode == "004380-0" ? 56_530m : 51_757m;
        }
    }
}
