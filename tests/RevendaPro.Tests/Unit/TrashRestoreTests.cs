using FluentAssertions;
using MediatR;
using Moq;
using RevendaPro.Application.Trash.Commands;
using RevendaPro.Application.Trash.Handlers;
using RevendaPro.Domain.Entities;
using RevendaPro.Domain.Enums;
using RevendaPro.Domain.Interfaces;
using RevendaPro.Domain.Interfaces.Repositories;
using RevendaPro.Domain.Interfaces.Security;
using RevendaPro.Shared.Exceptions;

namespace RevendaPro.Tests.Unit
{
    /// <summary>
    /// A volta da lixeira (M23).
    ///
    /// O que se prova: devolver um carro devolve <b>só a linha dele</b> — a ficha volta junto
    /// porque toda consulta dela passa pelo carro, e o que foi apagado um a um continua
    /// apagado; a placa reusada recusa a volta e <b>diz de quem ela é</b>; e um gasto só volta
    /// para um carro que esteja no pátio, porque devolvê-lo a um carro excluído o deixaria
    /// ativo no banco e ausente de toda tela.
    /// </summary>
    public class TrashRestoreTests
    {
        private const int IdTenant = 7;

        [Fact]
        public async Task OCarroDevolvido_VoltaAoPatio_ETemAVoltaRegistrada()
        {
            var world = new World();
            var fox = world.GivenDeletedVehicle();

            await world.Restore(TrashKind.Vehicle, fox.Code);

            fox.IsActive.Should().BeTrue();

            world.Vehicles.Verify(repository => repository.Update(fox), Times.Once);

            world.Audit.Verify(
                repository => repository.Add(It.Is<AuditLog>(log =>
                    log.EntityName == nameof(Vehicle)
                    && log.RecordCode == fox.Code
                    && log.Action == AuditAction.Activate)),
                Times.Once);

            world.UnitOfWork.Verify(
                unit => unit.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ADevolucaoDoCarro_JamaisTocaNumFilho()
        {
            var world = new World();
            var fox = world.GivenDeletedVehicle();

            // Um gasto que alguém apagou de propósito, antes de o carro sair.
            var gasto = world.GivenDeletedExpense(fox);

            await world.Restore(TrashKind.Vehicle, fox.Code);

            // A ficha volta porque a consulta dela passa pelo carro; o gasto apagado à parte
            // continua apagado. Percorrer os filhos ressuscitaria a foto que alguém tirou de lá
            // na semana passada.
            gasto.IsActive.Should().BeFalse();

            world.Expenses.Verify(
                repository => repository.Update(It.IsAny<VehicleExpense>()), Times.Never);
        }

        [Fact]
        public async Task APlacaJaCadastradaDeNovo_RecusaAVolta_EDizDeQuemEla()
        {
            var world = new World();
            var fox = world.GivenDeletedVehicle();

            world.GivenActiveVehicleWith(fox.Plate, "Fiat", "Uno", 2015);

            var act = () => world.Restore(TrashKind.Vehicle, fox.Code);

            var refusal = await act.Should().ThrowAsync<BusinessRuleException>();

            refusal.WithMessage($"*{fox.Plate}*").And.Message.Should().Contain(
                "Fiat Uno 2015", "a recusa nomeia o culpado, senão a pessoa fica sem saída");

            fox.IsActive.Should().BeFalse();

            world.UnitOfWork.Verify(
                unit => unit.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task OChassiJaCadastradoDeNovo_RecusaAVolta_EDizQueEOChassi()
        {
            var world = new World();
            var fox = world.GivenDeletedVehicle();

            world.GivenActiveVehicleWith("XYZ9K88", "Fiat", "Uno", 2015, chassis: fox.Chassis);

            var act = () => world.Restore(TrashKind.Vehicle, fox.Code);

            await act.Should().ThrowAsync<BusinessRuleException>()
                .WithMessage("*chassi*");
        }

        [Fact]
        public async Task OCarroQueJaEstaNoPatio_RecusaAVolta()
        {
            var world = new World();
            var fox = world.GivenDeletedVehicle();

            await world.Restore(TrashKind.Vehicle, fox.Code);

            var act = () => world.Restore(TrashKind.Vehicle, fox.Code);

            await act.Should().ThrowAsync<BusinessRuleException>()
                .WithMessage("*já está no pátio*");
        }

        [Fact]
        public async Task OGastoDeUmCarroQueEstaNoPatio_Volta()
        {
            var world = new World();
            var fox = world.GivenDeletedVehicle();

            fox.Activate("alguém");

            var gasto = world.GivenDeletedExpense(fox);

            await world.Restore(TrashKind.Expense, gasto.Code);

            gasto.IsActive.Should().BeTrue();

            world.Audit.Verify(
                repository => repository.Add(It.Is<AuditLog>(log =>
                    log.EntityName == nameof(VehicleExpense)
                    && log.RecordCode == gasto.Code
                    && log.Action == AuditAction.Activate)),
                Times.Once);
        }

        [Fact]
        public async Task OGastoDeUmCarroQueContinuaNaLixeira_ERecusado_EARecusaDizAOrdem()
        {
            var world = new World();
            var fox = world.GivenDeletedVehicle();
            var gasto = world.GivenDeletedExpense(fox);

            var act = () => world.Restore(TrashKind.Expense, gasto.Code);

            var refusal = await act.Should().ThrowAsync<BusinessRuleException>();

            refusal.And.Message.Should().Contain("Volkswagen Fox").And.Contain(
                "Devolva o carro primeiro",
                "um gasto devolvido a um carro excluído voltaria invisível");

            gasto.IsActive.Should().BeFalse();

            world.UnitOfWork.Verify(
                unit => unit.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task OGastoDeOutraRevenda_ERecusadoComoInexistente()
        {
            var world = new World(tenantOfTheVehicle: 8);
            var fox = world.GivenDeletedVehicle();

            fox.Activate("alguém");

            var gasto = world.GivenDeletedExpense(fox);

            var act = () => world.Restore(TrashKind.Expense, gasto.Code);

            // O gasto carrega revenda nenhuma: ele pende do carro, e é o carro que diz de quem
            // ele é (RNF-04).
            await act.Should().ThrowAsync<NotFoundException>();

            gasto.IsActive.Should().BeFalse();
        }

        [Fact]
        public async Task ODocumento_ContinuaVoltandoPelaPortaQueJaExistia()
        {
            var world = new World();
            var code = Guid.CreateVersion7();

            await world.Restore(TrashKind.Document, code);

            // A lixeira reaproveita o caminho do M10 em vez de escrever um segundo: dois
            // caminhos de escrita para a mesma linha são duas regras para manter em dia.
            world.Mediator.Verify(
                mediator => mediator.Send(
                    It.Is<Application.Vehicles.Commands.RestoreVehicleDocumentCommand>(
                        command => command.Code == code),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        private sealed class World
        {
            private readonly int tenantOfTheVehicle;
            private readonly List<Vehicle> yard = [];
            private readonly List<VehicleExpense> spending = [];
            private Vehicle? deletedVehicle;

            public World(int tenantOfTheVehicle = IdTenant)
            {
                this.tenantOfTheVehicle = tenantOfTheVehicle;

                var currentUser = new Mock<ICurrentUser>();
                currentUser.SetupGet(user => user.Id).Returns(9);
                currentUser.SetupGet(user => user.Code).Returns(Guid.CreateVersion7());
                currentUser.SetupGet(user => user.IdTenant).Returns(IdTenant);
                CurrentUser = currentUser;

                Vehicles = new Mock<IVehicleRepository>();
                Vehicles
                    .Setup(repository => repository.GetByCodeIncludingDeletedAsync(
                        IdTenant, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((int _, Guid code, CancellationToken _) =>
                        deletedVehicle?.Code == code ? deletedVehicle : null);
                Vehicles
                    .Setup(repository => repository.GetByIdIncludingDeletedAsync(
                        It.IsAny<int>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((int id, CancellationToken _) =>
                        deletedVehicle?.Id == id ? deletedVehicle : null);
                Vehicles
                    .Setup(repository => repository.FindActiveByIdentifierAsync(
                        IdTenant, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((int _, string plate, string chassis, CancellationToken _) =>
                        yard.Find(car => car.Plate == plate || car.Chassis == chassis));

                Expenses = new Mock<IVehicleExpenseRepository>();
                Expenses
                    .Setup(repository => repository.GetByCodeIncludingDeletedAsync(
                        It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((Guid code, CancellationToken _) =>
                        spending.Find(expense => expense.Code == code));

                Audit = new Mock<IAuditLogRepository>();
                Mediator = new Mock<IMediator>();

                var unitOfWork = new Mock<IUnitOfWork>();
                unitOfWork.SetupGet(unit => unit.VehicleRepository).Returns(Vehicles.Object);
                unitOfWork.SetupGet(unit => unit.VehicleExpenseRepository).Returns(Expenses.Object);
                unitOfWork.SetupGet(unit => unit.AuditLogRepository).Returns(Audit.Object);
                UnitOfWork = unitOfWork;
            }

            public Mock<IVehicleRepository> Vehicles { get; }

            public Mock<IVehicleExpenseRepository> Expenses { get; }

            public Mock<IAuditLogRepository> Audit { get; }

            public Mock<IMediator> Mediator { get; }

            public Mock<IUnitOfWork> UnitOfWork { get; }

            public Mock<ICurrentUser> CurrentUser { get; }

            public Vehicle GivenDeletedVehicle()
            {
                deletedVehicle = Vehicle.Create(
                    tenantOfTheVehicle, "LXA4D74", "9BWZZZ377VT004251",
                    "Volkswagen", "Fox", 2015, 2014);

                deletedVehicle.Id = 42;
                deletedVehicle.SoftDelete("quem apagou");

                return deletedVehicle;
            }

            public void GivenActiveVehicleWith(
                string plate, string brand, string model, short modelYear, string? chassis = null)
            {
                var car = Vehicle.Create(
                    IdTenant, plate, chassis ?? "9BWZZZ377VT009999",
                    brand, model, modelYear, (short)(modelYear - 1));

                car.Id = 43;
                yard.Add(car);
            }

            public VehicleExpense GivenDeletedExpense(Vehicle of)
            {
                var expense = VehicleExpense.Create(
                    of.Id, "Troca da embreagem", 3, 3_200m, new DateOnly(2026, 8, 20));

                expense.SoftDelete("quem apagou");
                spending.Add(expense);

                return expense;
            }

            public Task Restore(TrashKind kind, Guid code) =>
                new RestoreDeletedItemHandler(
                        UnitOfWork.Object, CurrentUser.Object, Mediator.Object)
                    .Handle(new RestoreDeletedItemCommand(kind, code), CancellationToken.None);
        }
    }
}
