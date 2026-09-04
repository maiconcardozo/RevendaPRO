using FluentAssertions;
using Moq;
using RevendaPro.Application.Yards.Commands;
using RevendaPro.Application.Yards.Handlers;
using RevendaPro.Domain.Entities;
using RevendaPro.Domain.Enums;
using RevendaPro.Domain.Interfaces;
using RevendaPro.Domain.Interfaces.Repositories;
using RevendaPro.Domain.Interfaces.Security;
using RevendaPro.Shared.Exceptions;

namespace RevendaPro.Tests.Unit
{
    /// <summary>
    /// O cadastro dos lugares onde o carro fica.
    ///
    /// <i>"Tudo seria pátio. Ele vai cadastrar um pátio particular ou um pátio Loja do
    /// Joãozinho. São os mesmos carros com as mesmas configurações — pagar ou não comissão."</i>
    ///
    /// É um cadastro só, com um tipo dentro, e o que se prova aqui é o que o tipo muda: pátio da
    /// casa jamais cobra da casa, e o repasse é combinado de um jeito só.
    /// </summary>
    public class YardTests
    {
        private const int IdTenant = 7;

        [Fact]
        public void AnOwnYard_NeverCarriesACut()
        {
            var yard = Yard.Create(IdTenant, "Pátio Centro", YardKind.Own);

            var act = () => yard.SetCut(cutPercent: 5m, cutAmount: null);

            act.Should().Throw<BusinessRuleException>().WithMessage("*sem repasse*");
        }

        [Fact]
        public void TurningAPartnerIntoAnOwnYard_LetsGoOfTheCut()
        {
            var yard = Yard.Create(IdTenant, "Loja do Joãozinho", YardKind.Partner);
            yard.SetCut(cutPercent: 8m, cutAmount: null);

            yard.SetKind(YardKind.Own);

            // Um percentual esquecido ali apareceria preenchido numa venda que jamais deveria
            // ter repasse — a loja virou pátio da casa, e a casa não cobra da casa.
            yard.CutPercent.Should().BeNull();
            yard.CutAmount.Should().BeNull();
        }

        [Fact]
        public void TheCut_IsAgreedInOneWayOnly()
        {
            var yard = Yard.Create(IdTenant, "Loja do Joãozinho", YardKind.Partner);

            var act = () => yard.SetCut(cutPercent: 8m, cutAmount: 4_000m);

            // Combinar das duas formas deixa a venda sem saber qual usar — a mesma regra que a
            // proposta e a venda seguem desde o M8.
            act.Should().Throw<BusinessRuleException>().WithMessage("*percentual ou em valor*");
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(101)]
        public void AnImpossibleCut_IsRefused(decimal percent)
        {
            var yard = Yard.Create(IdTenant, "Loja do Joãozinho", YardKind.Partner);

            var act = () => yard.SetCut(percent, cutAmount: null);

            act.Should().Throw<BusinessRuleException>();
        }

        [Fact]
        public void ThePhone_IsKeptAsDigits()
        {
            var yard = Yard.Create(IdTenant, "Loja do Joãozinho", YardKind.Partner);

            yard.SetContact("Joãozinho", "(47) 99988-7766", notes: null);

            yard.ContactPhone.Should().Be("47999887766");
        }

        [Fact]
        public void AYardWithoutAName_IsRefused()
        {
            var act = () => Yard.Create(IdTenant, "   ", YardKind.Own);

            act.Should().Throw<BusinessRuleException>().WithMessage("*nome do pátio*");
        }

        [Fact]
        public async Task AYardWithCarsInIt_RefusesDeletion_AndSaysHowMany()
        {
            var world = new World();
            var yard = world.Given("Loja do Joãozinho", YardKind.Partner, vehicles: 3);

            var act = () => world.Delete(yard.Code);

            // Recusa com o número, e não com "está em uso": quem lê precisa saber o tamanho do
            // trabalho de mover os carros antes de decidir.
            await act.Should().ThrowAsync<BusinessRuleException>().WithMessage("*3 carros*");
        }

        [Fact]
        public async Task AnEmptyYard_IsDeletedLogically_AndTheDeletionIsRecorded()
        {
            var world = new World();
            var yard = world.Given("Pátio Antigo", YardKind.Own, vehicles: 0);

            await world.Delete(yard.Code);

            // A exclusão lógica em si é do Foundation, e tem teste próprio em DomainRulesTests.
            // O que se afirma aqui é o trabalho deste handler: ele pede a remoção, e registra.
            world.Yards.Verify(
                repository => repository.Remove(yard, It.IsAny<string>()), Times.Once);

            world.Audit.Verify(
                repository => repository.Add(It.Is<AuditLog>(log =>
                    log.EntityName == nameof(Yard)
                    && log.RecordCode == yard.Code
                    && log.Action == AuditAction.Delete)),
                Times.Once);
        }

        [Fact]
        public async Task ANameAlreadyInUse_IsRefused()
        {
            var world = new World();
            world.Given("Pátio Centro", YardKind.Own, vehicles: 0);
            world.TheNameIsTaken();

            var act = () => world.Save("Pátio Centro", YardKind.Own);

            await act.Should().ThrowAsync<BusinessRuleException>().WithMessage("*já tem um pátio*");
        }

        private sealed class World
        {
            private readonly List<Yard> yards = [];
            private readonly Dictionary<int, int> vehiclesIn = [];
            private bool nameTaken;
            private int nextId = 1;

            public World()
            {
                Yards = new Mock<IYardRepository>();

                Yards.Setup(repository => repository.GetByCodeAsync(
                        IdTenant, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((int _, Guid code, CancellationToken _) =>
                        yards.FirstOrDefault(yard => yard.Code == code));

                Yards.Setup(repository => repository.CountVehiclesAsync(
                        It.IsAny<int>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((int id, CancellationToken _) =>
                        vehiclesIn.TryGetValue(id, out var count) ? count : 0);

                Yards.Setup(repository => repository.NameExistsAsync(
                        It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int?>(),
                        It.IsAny<CancellationToken>()))
                    .ReturnsAsync(() => nameTaken);

                Audit = new Mock<IAuditLogRepository>();

                UnitOfWork = new Mock<IUnitOfWork>();
                UnitOfWork.SetupGet(unit => unit.YardRepository).Returns(Yards.Object);
                UnitOfWork.SetupGet(unit => unit.AuditLogRepository).Returns(Audit.Object);

                var currentUser = new Mock<ICurrentUser>();
                currentUser.SetupGet(user => user.Id).Returns(9);
                currentUser.SetupGet(user => user.IdTenant).Returns(IdTenant);
                currentUser.SetupGet(user => user.Code).Returns(Guid.NewGuid());

                Saver = new SaveYardHandler(UnitOfWork.Object, currentUser.Object);
                Remover = new DeleteYardHandler(UnitOfWork.Object, currentUser.Object);
            }

            public Mock<IUnitOfWork> UnitOfWork { get; }

            public Mock<IYardRepository> Yards { get; }

            public Mock<IAuditLogRepository> Audit { get; }

            private SaveYardHandler Saver { get; }

            private DeleteYardHandler Remover { get; }

            public Yard Given(string name, YardKind kind, int vehicles)
            {
                var yard = Yard.Create(IdTenant, name, kind);
                yard.Id = nextId++;

                yards.Add(yard);
                vehiclesIn[yard.Id] = vehicles;

                return yard;
            }

            public void TheNameIsTaken() => nameTaken = true;

            public Task<Application.Yards.DTOs.YardDto> Save(string name, YardKind kind) =>
                Saver.Handle(
                    new SaveYardCommand(null, name, kind, null, null, null, null, null, 0),
                    CancellationToken.None);

            public Task Delete(Guid code) =>
                Remover.Handle(new DeleteYardCommand(code), CancellationToken.None);
        }
    }
}
