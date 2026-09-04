using FluentAssertions;
using Moq;
using RevendaPro.Application.Vehicles.Commands;
using RevendaPro.Application.Vehicles.Handlers;
using RevendaPro.Domain.Entities;
using RevendaPro.Domain.Enums;
using RevendaPro.Domain.Interfaces;
using RevendaPro.Domain.Interfaces.Repositories;
using RevendaPro.Domain.Interfaces.Security;
using RevendaPro.Shared.Exceptions;

namespace RevendaPro.Tests.Unit
{
    /// <summary>
    /// O carro mudando de lugar, e a passagem ficando registrada.
    ///
    /// <i>"O carro ainda é dele, só está em pátio diferente."</i>
    ///
    /// A pergunta que isto responde depois é a que decide o negócio: o carro ficou dois meses
    /// na Loja do Joãozinho e voltou sem vender. Sem a passagem escrita, o sistema só sabe onde
    /// o carro está hoje, e jamais por onde ele andou.
    /// </summary>
    public class VehicleYardTests
    {
        private const int IdTenant = 7;
        private const int PatioCentro = 1;
        private const int LojaDoJoaozinho = 2;

        [Fact]
        public void ACarThatChangesYard_SaysWhereItCameFrom()
        {
            var cruze = Car();
            cruze.MoveToYard(PatioCentro);

            var left = cruze.MoveToYard(LojaDoJoaozinho);

            // Quem responde de onde o carro veio é a entidade, e não quem chama: só ela sabia o
            // valor anterior, e é esse valor que a passagem guarda.
            left.Should().Be(PatioCentro);
            cruze.IdYard.Should().Be(LojaDoJoaozinho);
        }

        [Fact]
        public void ACarAlreadyInThatYard_IsRefused()
        {
            var cruze = Car();
            cruze.MoveToYard(LojaDoJoaozinho);

            var act = () => cruze.MoveToYard(LojaDoJoaozinho);

            // Aceitar isto escreveria uma passagem que sai e chega no mesmo lugar, e a linha do
            // tempo passaria a contar mudanças que jamais aconteceram.
            act.Should().Throw<BusinessRuleException>().WithMessage("*já está neste pátio*");
        }

        [Fact]
        public async Task MovingACar_RecordsThePassage_WithWhereFromAndWhy()
        {
            var world = new World();
            world.TheCarIsIn(PatioCentro);

            await world.Move(world.LojaDoJoaozinho.Code, "Exposição de fim de semana");

            world.History.Verify(
                repository => repository.Add(It.Is<VehicleYardHistory>(passage =>
                    passage.IdVehicle == world.Cruze.Id
                    && passage.IdFromYard == PatioCentro
                    && passage.IdToYard == LojaDoJoaozinho
                    && passage.Reason == "Exposição de fim de semana")),
                Times.Once);

            world.Vehicles.Verify(
                repository => repository.Update(world.Cruze), Times.Once);
        }

        [Fact]
        public async Task TakingACarOutOfEveryYard_IsAPassageToo()
        {
            var world = new World();
            world.TheCarIsIn(LojaDoJoaozinho);

            await world.Move(yardCode: null, reason: "Voltou sem vender");

            // Tirar o carro do pátio do parceiro é exatamente o fato que interessa medir depois,
            // e ele se perderia se só a ida virasse evento.
            world.History.Verify(
                repository => repository.Add(It.Is<VehicleYardHistory>(passage =>
                    passage.IdFromYard == LojaDoJoaozinho && passage.IdToYard == null)),
                Times.Once);

            world.Cruze.IdYard.Should().BeNull();
        }

        [Fact]
        public async Task AYardOfAnotherDealership_IsRefused_AndMovesNothing()
        {
            var world = new World();
            world.TheCarIsIn(PatioCentro);

            var act = () => world.Move(Guid.NewGuid(), reason: null);

            // O pátio é procurado por código <b>e</b> por cliente, juntos. Um código de outra
            // revenda simplesmente inexiste aqui — e o carro fica onde estava.
            await act.Should().ThrowAsync<NotFoundException>();

            world.Cruze.IdYard.Should().Be(PatioCentro);

            world.History.Verify(
                repository => repository.Add(It.IsAny<VehicleYardHistory>()), Times.Never);
        }

        private static Vehicle Car()
        {
            var cruze = Vehicle.Create(
                IdTenant, "ABC1D23", "9BWZZZ377VT004251", "Chevrolet", "Cruze", 2014, 2013);

            cruze.Id = 42;

            return cruze;
        }

        private sealed class World
        {
            public World()
            {
                Cruze = Car();

                PatioCentroYard = Yard.Create(IdTenant, "Pátio Centro", YardKind.Own);
                PatioCentroYard.Id = PatioCentro;

                LojaDoJoaozinho = Yard.Create(IdTenant, "Loja do Joãozinho", YardKind.Partner);
                LojaDoJoaozinho.Id = VehicleYardTests.LojaDoJoaozinho;

                Vehicles = new Mock<IVehicleRepository>();
                Vehicles.Setup(repository => repository.GetByCodeAsync(
                        IdTenant, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(Cruze);

                var yards = new Mock<IYardRepository>();
                yards.Setup(repository => repository.GetByCodeAsync(
                        IdTenant, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((int _, Guid code, CancellationToken _) =>
                        new[] { PatioCentroYard, LojaDoJoaozinho }
                            .FirstOrDefault(yard => yard.Code == code));

                History = new Mock<IVehicleYardHistoryRepository>();

                var auditLogs = new Mock<IAuditLogRepository>();

                var unitOfWork = new Mock<IUnitOfWork>();
                unitOfWork.SetupGet(unit => unit.VehicleRepository).Returns(Vehicles.Object);
                unitOfWork.SetupGet(unit => unit.YardRepository).Returns(yards.Object);
                unitOfWork.SetupGet(unit => unit.VehicleYardHistoryRepository).Returns(History.Object);
                unitOfWork.SetupGet(unit => unit.AuditLogRepository).Returns(auditLogs.Object);

                var currentUser = new Mock<ICurrentUser>();
                currentUser.SetupGet(user => user.Id).Returns(9);
                currentUser.SetupGet(user => user.IdTenant).Returns(IdTenant);
                currentUser.SetupGet(user => user.Code).Returns(Guid.NewGuid());

                Mover = new MoveVehicleToYardHandler(unitOfWork.Object, currentUser.Object);
            }

            public Vehicle Cruze { get; }

            public Yard PatioCentroYard { get; }

            public Yard LojaDoJoaozinho { get; }

            public Mock<IVehicleRepository> Vehicles { get; }

            public Mock<IVehicleYardHistoryRepository> History { get; }

            private MoveVehicleToYardHandler Mover { get; }

            public void TheCarIsIn(int idYard) => Cruze.MoveToYard(idYard);

            public Task Move(Guid? yardCode, string? reason) =>
                Mover.Handle(
                    new MoveVehicleToYardCommand(Cruze.Code, yardCode, reason),
                    CancellationToken.None);
        }
    }
}
