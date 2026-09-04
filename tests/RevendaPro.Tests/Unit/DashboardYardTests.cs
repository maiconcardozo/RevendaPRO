using FluentAssertions;
using Moq;
using RevendaPro.Application.Dashboard.DTOs;
using RevendaPro.Application.Dashboard.Handlers;
using RevendaPro.Application.Dashboard.Queries;
using RevendaPro.Domain.Entities;
using RevendaPro.Domain.Enums;
using RevendaPro.Domain.Interfaces;
using RevendaPro.Domain.Interfaces.Repositories;
using RevendaPro.Domain.Interfaces.Security;
using RevendaPro.Domain.Interfaces.Storage;
using RevendaPro.Domain.ValueObjects;

namespace RevendaPro.Tests.Unit
{
    /// <summary>
    /// O painel respondendo por lugar sem parar de responder pelo todo.
    ///
    /// <i>"Ele precisa tirar relatório de cada pátio ou revenda, e um todo junto, mas sempre
    /// agrupado."</i>
    ///
    /// A frase tem duas metades, e a decisão do V0 foi atender as duas: o agrupamento entra
    /// como um bloco a mais, e os números do topo continuam somando o estoque inteiro. Trocar o
    /// total por um filtro obrigaria a pessoa a escolher entre a parte e o todo.
    /// </summary>
    public class DashboardYardTests
    {
        private const int IdTenant = 7;
        private static readonly DateOnly Compra = new(2026, 8, 1);

        [Fact]
        public async Task ThePanel_AnswersForEachYard_AndForEverythingTogether()
        {
            var world = new World();
            var centro = world.GivenYard(id: 1, name: "Pátio Centro", position: 1);
            var loja = world.GivenYard(id: 2, name: "Loja do Joãozinho", position: 2);

            world.GivenVehicle(id: 10, purchase: 30_000m, idYard: centro.Id);
            world.GivenVehicle(id: 11, purchase: 20_000m, idYard: loja.Id);
            world.GivenVehicle(id: 12, purchase: 25_000m, idYard: loja.Id);

            var panel = await world.Read();

            var porPatio = panel.ByYard.ToDictionary(row => row.Name);

            porPatio["Pátio Centro"].Count.Should().Be(1);
            porPatio["Pátio Centro"].Invested.Should().Be(30_000m);

            porPatio["Loja do Joãozinho"].Count.Should().Be(2);
            porPatio["Loja do Joãozinho"].Invested.Should().Be(45_000m);

            // A outra metade da frase: o topo continua somando tudo, e é a soma dos pedaços.
            panel.InStock.Should().Be(3);
            panel.Invested.Should().Be(75_000m);
            panel.ByYard.Sum(row => row.Invested).Should().Be(panel.Invested);
        }

        [Fact]
        public async Task ASoldCar_LeavesTheYardItWasParkedIn()
        {
            var world = new World();
            var loja = world.GivenYard(id: 2, name: "Loja do Joãozinho", position: 1);

            world.GivenVehicle(id: 11, purchase: 20_000m, idYard: loja.Id);
            world.GivenVehicle(id: 12, purchase: 25_000m, idYard: loja.Id, sold: true);

            var panel = await world.Read();

            // Mesmo motivo do número do topo: o dinheiro do carro vendido voltou, e contá-lo
            // como parado na loja diria que há capital preso onde já não há.
            panel.ByYard.Single().Count.Should().Be(1);
            panel.ByYard.Single().Invested.Should().Be(20_000m);
        }

        [Fact]
        public async Task AnEmptyYard_StaysOnTheList_AndCarsWithoutAPlace_GetTheirOwnLine()
        {
            var world = new World();
            world.GivenYard(id: 1, name: "Pátio Centro", position: 1);
            world.GivenYard(id: 2, name: "Loja do Joãozinho", position: 2);

            world.GivenVehicle(id: 10, purchase: 30_000m, idYard: null);

            var panel = await world.Read();

            // "Zero carro na Loja do Joãozinho" é uma resposta. Sumir da tela seria confundido
            // com um pátio que ninguém cadastrou.
            panel.ByYard.Should().HaveCount(3);
            panel.ByYard[0].Name.Should().Be("Pátio Centro");
            panel.ByYard[1].Name.Should().Be("Loja do Joãozinho");
            panel.ByYard[1].Count.Should().Be(0);

            // E o carro sem lugar jamais some da conta: ele é a diferença entre o total e a
            // soma dos pátios, e sem esta linha ninguém encontraria os R$ 30.000.
            panel.ByYard[2].Name.Should().Be("Sem pátio");
            panel.ByYard[2].Code.Should().BeNull();
            panel.ByYard[2].Invested.Should().Be(30_000m);
        }

        [Fact]
        public async Task TheAverageDaysParked_IsAnsweredPerYard()
        {
            var world = new World();
            var loja = world.GivenYard(id: 2, name: "Loja do Joãozinho", position: 1);

            var hoje = DateOnly.FromDateTime(DateTime.UtcNow);

            world.GivenVehicle(id: 11, purchase: 20_000m, idYard: loja.Id, purchasedOn: hoje.AddDays(-20));
            world.GivenVehicle(id: 12, purchase: 25_000m, idYard: loja.Id, purchasedOn: hoje.AddDays(-40));

            var panel = await world.Read();

            // É o número que decide se vale deixar carro lá: 20 e 40 dias parados dão 30.
            panel.ByYard.Single().AverageDaysInStock.Should().Be(30);
        }

        private sealed class World
        {
            private readonly List<Vehicle> vehicles = [];
            private readonly List<Yard> yards = [];

            public World()
            {
                var currentUser = new Mock<ICurrentUser>();
                currentUser.SetupGet(user => user.IdTenant).Returns(IdTenant);

                var vehicleRepository = new Mock<IVehicleRepository>();
                vehicleRepository.Setup(repository => repository.ListAsync(
                        IdTenant, It.IsAny<string?>(), It.IsAny<VehicleStatus?>(),
                        It.IsAny<VehicleOrigin?>(), It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(),
                        It.IsAny<int?>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(() => vehicles);

                var sales = new Mock<ISaleRepository>();
                sales.Setup(repository => repository.ListByTenantAsync(
                        IdTenant, It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(),
                        It.IsAny<CancellationToken>()))
                    .ReturnsAsync([]);

                var expenses = new Mock<IVehicleExpenseRepository>();
                expenses.Setup(repository => repository.ListByVehiclesAsync(
                        It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync([]);

                var photos = new Mock<IVehiclePhotoRepository>();
                photos.Setup(repository => repository.SummarizeAsync(
                        It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync([]);

                var yardRepository = new Mock<IYardRepository>();
                yardRepository.Setup(repository => repository.ListByTenantAsync(
                        IdTenant, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(() => yards);

                var unitOfWork = new Mock<IUnitOfWork>();
                unitOfWork.SetupGet(unit => unit.VehicleRepository).Returns(vehicleRepository.Object);
                unitOfWork.SetupGet(unit => unit.SaleRepository).Returns(sales.Object);
                unitOfWork.SetupGet(unit => unit.VehicleExpenseRepository).Returns(expenses.Object);
                unitOfWork.SetupGet(unit => unit.VehiclePhotoRepository).Returns(photos.Object);
                unitOfWork.SetupGet(unit => unit.YardRepository).Returns(yardRepository.Object);

                Handler = new GetDashboardHandler(
                    unitOfWork.Object, currentUser.Object, new Mock<IFileStorage>().Object);
            }

            private GetDashboardHandler Handler { get; }

            public Yard GivenYard(int id, string name, int position)
            {
                var yard = Yard.Create(IdTenant, name, YardKind.Own, position);
                yard.Id = id;

                yards.Add(yard);

                return yard;
            }

            public void GivenVehicle(
                int id,
                decimal purchase,
                int? idYard,
                bool sold = false,
                DateOnly? purchasedOn = null)
            {
                var vehicle = Vehicle.Create(
                    IdTenant, $"ABC1D{id:00}", $"9BWZZZ377VT0042{id:00}",
                    "Chevrolet", "Cruze", 2014, 2013);

                vehicle.Id = id;
                vehicle.SetPurchase(purchase, purchasedOn ?? Compra, null, null);

                if (idYard is not null)
                {
                    vehicle.MoveToYard(idYard);
                }

                if (sold)
                {
                    // A esteira inteira até a porta da venda. Vendido só se alcança por Sell,
                    // porque um carro vendido sem venda por trás ficaria sem comprador, sem
                    // preço e sem lucro.
                    vehicle.ChangeStatus(VehicleStatus.Purchased);
                    vehicle.ChangeStatus(VehicleStatus.ReadyForSale);
                    vehicle.Sell();
                }

                vehicles.Add(vehicle);
            }

            public Task<DashboardDto> Read() =>
                Handler.Handle(new GetDashboardQuery(null, null), CancellationToken.None);
        }
    }
}
