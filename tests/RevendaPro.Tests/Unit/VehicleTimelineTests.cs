using FluentAssertions;
using Moq;
using RevendaPro.Application.Vehicles.Handlers;
using RevendaPro.Application.Vehicles.Queries;
using RevendaPro.Domain.Entities;
using RevendaPro.Domain.Enums;
using RevendaPro.Domain.Interfaces;
using RevendaPro.Domain.Interfaces.Repositories;
using RevendaPro.Domain.Interfaces.Security;
using RevendaPro.Domain.ValueObjects;
using RevendaPro.Shared.Exceptions;

namespace RevendaPro.Tests.Unit
{
    /// <summary>
    /// A história única do veículo (RF-26), no nível do caso de uso.
    ///
    /// A consulta que reúne os eventos é SQL, e o banco a prova. O que se prova aqui é o que
    /// só o caso de uso decide: quem é o autor de cada evento, e o que acontece quando esse
    /// autor saiu da revenda ou nunca existiu. Nada aqui toca banco nem rede.
    /// </summary>
    public class VehicleTimelineTests
    {
        private const int IdTenant = 7;
        private static readonly DateTime Moment = new(2026, 8, 15, 10, 0, 0, DateTimeKind.Utc);

        [Fact]
        public async Task Timeline_TurnsTheCodeStoredInEveryTable_IntoTheNameAPersonRecognizes()
        {
            var world = new World();
            var ana = world.GivenUser("Ana");

            world.GivenEvents(
                Entry(TimelineEventKind.Expense, ana.Code.ToString(), title: "Funilaria", amount: 350m));

            var timeline = await world.Read();

            timeline.Should().ContainSingle()
                .Which.ActorName.Should().Be("Ana");
        }

        [Fact]
        public async Task Timeline_KeepsTheEvent_WhenNobodyAnswersForTheAuthor()
        {
            var world = new World();

            // O sistema semeia linhas, e um código pode sobrar de uma empresa que já saiu.
            // O que aconteceu importa mais do que quem digitou: o evento continua na história.
            world.GivenEvents(Entry(TimelineEventKind.Expense, actorCode: "SYSTEM", title: "Frete"));

            var timeline = await world.Read();

            timeline.Should().ContainSingle();
            timeline[0].Title.Should().Be("Frete");
            timeline[0].ActorName.Should().BeNull();
        }

        [Fact]
        public async Task Timeline_NamesTheAuthorWhoLeftTheDealership()
        {
            var world = new World();
            var whoLeft = world.GivenUser("Bruno", deleted: true);

            world.GivenEvents(Entry(TimelineEventKind.Photos, whoLeft.Code.ToString(), quantity: 12));

            var timeline = await world.Read();

            // Uma história que esquece o autor no dia em que a conta é fechada é uma história
            // que se reescreve sozinha.
            timeline.Should().ContainSingle().Which.ActorName.Should().Be("Bruno");

            world.Users.Verify(
                repository => repository.ListByTenantAsync(
                    IdTenant, null, true, It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task Timeline_CarriesWhatEachKindOfEventHas_AndNothingMore()
        {
            var world = new World();
            var ana = world.GivenUser("Ana");

            world.GivenEvents(
                Entry(TimelineEventKind.Photos, ana.Code.ToString(), quantity: 20),
                Entry(TimelineEventKind.Expense, ana.Code.ToString(), title: "Pneu", amount: 490m, isPaid: true));

            var timeline = await world.Read();

            // As fotos do dia entram contadas, e sem código: o agrupamento representa vinte
            // registros, e nenhum deles em particular.
            var photos = timeline.Single(entry => entry.Kind == TimelineEventKind.Photos);
            photos.Quantity.Should().Be(20);
            photos.Code.Should().BeNull();
            photos.Amount.Should().BeNull();

            var expense = timeline.Single(entry => entry.Kind == TimelineEventKind.Expense);
            expense.Amount.Should().Be(490m);
            expense.IsPaid.Should().BeTrue();
            expense.Quantity.Should().Be(1);
        }

        [Fact]
        public async Task Timeline_OfACarOfAnotherDealership_IsRefused()
        {
            var world = new World(vehicleExists: false);

            var act = () => world.Read();

            // O veículo é buscado pelo tenant de quem pergunta, então um carro de outra
            // empresa simplesmente inexiste — e a linha do tempo nunca chega a ser lida.
            await act.Should().ThrowAsync<NotFoundException>();

            world.Vehicles.Verify(
                repository => repository.ListTimelineAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        private static VehicleTimelineEntry Entry(
            TimelineEventKind kind,
            string actorCode,
            string? title = null,
            decimal? amount = null,
            int quantity = 1,
            bool? isPaid = null) =>
            new(Moment, kind, Code: null, title, Detail: null, amount, quantity,
                FromStatus: null, ToStatus: null, ProposalStatus: null, isPaid, actorCode);

        private sealed class World
        {
            private readonly List<User> people = [];
            private readonly List<VehicleTimelineEntry> events = [];
            private readonly Vehicle vehicle;

            public World(bool vehicleExists = true)
            {
                vehicle = Vehicle.Create(
                    IdTenant, "ABC1D23", "9BWZZZ377VT004251", "Chevrolet", "Cruze", 2014, 2013);
                vehicle.Id = 42;

                var currentUser = new Mock<ICurrentUser>();
                currentUser.SetupGet(user => user.IdTenant).Returns(IdTenant);
                CurrentUser = currentUser;

                Vehicles = new Mock<IVehicleRepository>();
                Vehicles
                    .Setup(repository => repository.GetByCodeAsync(
                        IdTenant, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(vehicleExists ? vehicle : null);
                Vehicles
                    .Setup(repository => repository.ListTimelineAsync(
                        vehicle.Id, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(() => events);

                Users = new Mock<IUserRepository>();
                Users
                    .Setup(repository => repository.ListByTenantAsync(
                        IdTenant, It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(() => people);

                var unitOfWork = new Mock<IUnitOfWork>();
                unitOfWork.SetupGet(unit => unit.VehicleRepository).Returns(Vehicles.Object);
                unitOfWork.SetupGet(unit => unit.UserRepository).Returns(Users.Object);
                UnitOfWork = unitOfWork;
            }

            public Mock<IVehicleRepository> Vehicles { get; }

            public Mock<IUserRepository> Users { get; }

            public Mock<IUnitOfWork> UnitOfWork { get; }

            public Mock<ICurrentUser> CurrentUser { get; }

            public User GivenUser(string name, bool deleted = false)
            {
                var user = User.Create(IdTenant, name, $"{name}@revenda.com.br", "hash");

                if (deleted)
                {
                    user.SoftDelete(name);
                }

                people.Add(user);

                return user;
            }

            public void GivenEvents(params VehicleTimelineEntry[] entries) => events.AddRange(entries);

            public async Task<IReadOnlyList<Application.Vehicles.DTOs.VehicleTimelineEntryDto>> Read()
            {
                var handler = new GetVehicleTimelineHandler(UnitOfWork.Object, CurrentUser.Object);

                return await handler.Handle(
                    new GetVehicleTimelineQuery(vehicle.Code), CancellationToken.None);
            }
        }
    }
}
