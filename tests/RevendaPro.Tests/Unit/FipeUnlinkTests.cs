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
    /// Desfazer a consulta da tabela.
    ///
    /// <i>"E também ele poder desvincular a consulta removendo o código."</i>
    ///
    /// É a outra metade da decisão de que a escolha é sempre da pessoa: se quem aponta o carro
    /// para uma linha da tabela é ela, errar a linha precisa ter volta. O que se prova aqui é a
    /// decisão 3 do V0 do M16 — <b>sai tudo junto</b>, porque o valor veio do modelo que está
    /// sendo desfeito, e um preço sem nada que o explique alimentaria o painel de custo.
    /// </summary>
    public class FipeUnlinkTests
    {
        private const int IdTenant = 7;
        private const int OtherTenant = 8;
        private static readonly DateOnly Setembro = new(2026, 9, 1);

        [Fact]
        public async Task Unlinking_ErasesTheWholeLookup_AndNeverHalfOfIt()
        {
            var world = new World();
            var vehicle = world.GivenCarWithLookup();

            await world.Unlink(vehicle.Code);

            // Os quatro campos da ficha voltam a "—" juntos. Guardar o valor sem o modelo
            // deixaria na tela um preço que ninguém consegue explicar, e ele seguiria entrando
            // no painel de custo e na projeção de sobra como se fosse a tabela falando.
            vehicle.FipeCode.Should().BeNull();
            vehicle.FipeYearFuel.Should().BeNull();
            vehicle.FipeValue.Should().BeNull();
            vehicle.FipeReferenceDate.Should().BeNull();
            vehicle.FipeSource.Should().BeNull();

            world.Vehicles.Verify(repository => repository.Update(vehicle), Times.Once);
            world.UnitOfWork.Verify(
                unit => unit.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ThePricesOfTheDealership_StayExactlyWhereTheyWere()
        {
            var world = new World();
            var vehicle = world.GivenCarWithLookup();

            vehicle.SetPricing(70_000m, 66_000m, 74_900m, null);

            await world.Unlink(vehicle.Code);

            // A tabela jamais escreveu preço nenhum, e desfazê-la também jamais escreve. Quanto
            // a revenda quer receber, o mínimo que aceita e o anunciado são de quem entende do
            // carro — a mesma frase que o M11 segura desde que este assunto existe.
            vehicle.DesiredNetPrice.Should().Be(70_000m);
            vehicle.MinimumNetPrice.Should().Be(66_000m);
            vehicle.AdvertisedPrice.Should().Be(74_900m);
        }

        [Fact]
        public async Task AfterUnlinking_TheMonthlyRoutineNoLongerReachesTheCar()
        {
            var world = new World();
            var vehicle = world.GivenCarWithLookup();

            await world.Unlink(vehicle.Code);

            // A rotina do pátio só toca em carro com código. Desfazer é, também, a saída de
            // quem quer este carro fora dela — e isso é consequência desejada, e não efeito
            // colateral: o carro sem modelo escolhido volta a ser um carro sem tabela.
            vehicle.FipeCode.Should().BeNull();
            vehicle.AcceptsAutomaticFipe.Should().BeTrue();
        }

        [Fact]
        public async Task ACarWithNoLookup_IsRefusedWithAReason_AndNeverInSilence()
        {
            var world = new World();
            var vehicle = world.GivenCarWithoutLookup();

            var act = () => world.Unlink(vehicle.Code);

            // A tela que ofereceu o botão estava olhando uma ficha de antes. Responder em
            // silêncio deixaria a pessoa achando que desfez alguma coisa.
            await act.Should().ThrowAsync<BusinessRuleException>().WithMessage("*sem consulta*");

            world.UnitOfWork.Verify(
                unit => unit.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ACarOfAnotherDealership_IsRefused()
        {
            var world = new World(tenantOfTheCar: OtherTenant);
            var vehicle = world.GivenCarWithLookup();

            var act = () => world.Unlink(vehicle.Code);

            await act.Should().ThrowAsync<NotFoundException>();

            // E a ficha do carro da outra revenda continua inteira (RNF-04).
            vehicle.FipeCode.Should().Be("015123-4");
        }

        private sealed class World
        {
            private readonly int tenantOfTheCar;
            private readonly List<Vehicle> yard = [];

            public World(int tenantOfTheCar = IdTenant)
            {
                this.tenantOfTheCar = tenantOfTheCar;

                Vehicles = new Mock<IVehicleRepository>();
                Vehicles.Setup(repository => repository.GetByCodeAsync(
                        IdTenant, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((int _, Guid code, CancellationToken _) =>
                        yard.FirstOrDefault(vehicle => vehicle.Code == code));

                UnitOfWork = new Mock<IUnitOfWork>();
                UnitOfWork.SetupGet(unit => unit.VehicleRepository).Returns(Vehicles.Object);
                UnitOfWork.SetupGet(unit => unit.AuditLogRepository)
                    .Returns(new Mock<IAuditLogRepository>().Object);

                var currentUser = new Mock<ICurrentUser>();
                currentUser.SetupGet(user => user.IdTenant).Returns(IdTenant);
                currentUser.SetupGet(user => user.Code).Returns(Guid.NewGuid());

                Handler = new UnlinkVehicleFipeHandler(UnitOfWork.Object, currentUser.Object);
            }

            public Mock<IVehicleRepository> Vehicles { get; }

            public Mock<IUnitOfWork> UnitOfWork { get; }

            private UnlinkVehicleFipeHandler Handler { get; }

            public Vehicle GivenCarWithLookup()
            {
                var vehicle = GivenCarWithoutLookup();

                vehicle.ApplyFipeReference(
                    74_969.00m, Setembro, "015123-4", "2020-5", "alguem");

                return vehicle;
            }

            public Vehicle GivenCarWithoutLookup()
            {
                var vehicle = Vehicle.Create(
                    IdTenant, "ABC1D23", "9BWZZZ377VT004251", "Jeep", "Renegade", 2020, 2019);

                vehicle.SetDetails(
                    "1.8 Longitude", "Branco", FuelType.Flex, TransmissionType.Automatic, null, null);

                vehicle.Id = 42;

                if (tenantOfTheCar == IdTenant)
                {
                    yard.Add(vehicle);
                }

                return vehicle;
            }

            public Task Unlink(Guid code) =>
                Handler.Handle(new UnlinkVehicleFipeCommand(code), CancellationToken.None);
        }
    }
}
