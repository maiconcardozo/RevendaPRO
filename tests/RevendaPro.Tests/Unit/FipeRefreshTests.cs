using FluentAssertions;
using Moq;
using RevendaPro.Application.Fipe;
using RevendaPro.Application.Vehicles.Commands;
using RevendaPro.Application.Vehicles.Handlers;
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
    /// O botão que vai buscar a tabela, e o que ele tem permissão de escrever.
    ///
    /// A frase que define o marco inteiro é do stakeholder: <i>"a FIPE é referência para
    /// precificação, e jamais a precificação final; ela pode sugerir, mas o preço mesmo quem
    /// muda é o usuário"</i>. O primeiro teste aqui é a tradução literal disso — a consulta
    /// escreve valor, mês, modelo e origem, e deixa os três campos de preço exatamente como
    /// estavam.
    ///
    /// O resto guarda a outra promessa: a tabela jamais derruba a operação nem apaga o que a
    /// ficha já tinha.
    /// </summary>
    public class FipeRefreshTests
    {
        private const int IdTenant = 7;
        private const int OtherTenant = 8;

        private static readonly DateOnly Setembro = new(2026, 9, 1);

        [Fact]
        public async Task TheLookupWritesTheReference_AndTouchesNoPrice()
        {
            var world = new World();
            var vehicle = world.GivenVehicle(fipeCode: "004380-0", yearFuel: "2014-5");

            vehicle.SetPricing(58_000m, 55_000m, 61_000m, "Dois iguais a 62 na região.");

            var answer = await world.Refresh(vehicle.Code);

            answer.Value.Should().Be(56_530.00m);
            answer.ReferenceMonth.Should().Be(Setembro);
            answer.Source.Should().Be(FipeSource.Automatic);

            vehicle.FipeValue.Should().Be(56_530.00m);
            vehicle.FipeReferenceDate.Should().Be(Setembro);
            vehicle.FipeSource.Should().Be(FipeSource.Automatic);

            // O coração do marco: a tabela aparece ao lado do preço, e jamais dentro dele.
            vehicle.DesiredNetPrice.Should().Be(58_000m);
            vehicle.MinimumNetPrice.Should().Be(55_000m);
            vehicle.AdvertisedPrice.Should().Be(61_000m);
            vehicle.MarketNotes.Should().Be("Dois iguais a 62 na região.");
        }

        [Fact]
        public async Task TheLookupIsRecorded_AndSavedOnce()
        {
            var world = new World();
            var vehicle = world.GivenVehicle(fipeCode: "004380-0", yearFuel: "2014-5");

            await world.Refresh(vehicle.Code);

            world.Vehicles.Verify(repository => repository.Update(vehicle), Times.Once);

            world.Audit.Verify(
                repository => repository.Add(It.Is<AuditLog>(log =>
                    log.EntityName == nameof(Vehicle)
                    && log.RecordCode == vehicle.Code
                    && log.Action == AuditAction.Update)),
                Times.Once);

            // Um commit para o veículo e para a cotação que o leitor enfileirou: ou a tabela
            // respondeu e os dois entram, ou nenhum entra.
            world.UnitOfWork.Verify(
                unit => unit.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task TheYearFuelIsFoundFromTheModelYear_AndKeptForTheNextTime()
        {
            var world = new World();

            // Todo carro cadastrado antes deste marco tem código e ficou sem o par. Pedir para
            // alguém digitar "2014-5" seria pedir que a pessoa conheça a forma de um espelho.
            var vehicle = world.GivenVehicle(fipeCode: "004380-0", yearFuel: null);

            await world.Refresh(vehicle.Code);

            vehicle.FipeYearFuel.Should().Be("2014-5");

            world.Quotes.Verify(
                reader => reader.ResolveYearFuelAsync(
                    "004380-0", (short)2014, It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task AVehicleOfAnotherDealership_IsRefused()
        {
            var world = new World(tenantOfTheVehicle: OtherTenant);
            var vehicle = world.GivenVehicle(fipeCode: "004380-0", yearFuel: "2014-5");

            var act = () => world.Refresh(vehicle.Code);

            // A leitura é sempre pela empresa de quem pediu (RNF-04).
            await act.Should().ThrowAsync<NotFoundException>();
        }

        [Fact]
        public async Task WithoutTheModelCode_TheAnswerSaysWhatToDo()
        {
            var world = new World();
            var vehicle = world.GivenVehicle(fipeCode: null, yearFuel: null);

            var act = () => world.Refresh(vehicle.Code);

            // Recusa que ensina: a busca por marca e modelo é o próximo passo do marco, e até
            // lá o código vem da ficha.
            await act.Should().ThrowAsync<BusinessRuleException>()
                .WithMessage("*código da FIPE*");
        }

        [Fact]
        public async Task WithTheTableOutOfReach_TheSheetKeepsWhatItHad()
        {
            var world = new World();
            var vehicle = world.GivenVehicle(fipeCode: "004380-0", yearFuel: "2014-5");

            vehicle.SetFipe(66_000m, new DateOnly(2026, 7, 1), "004380-0");
            world.TheTableIsOutOfReach();

            var act = () => world.Refresh(vehicle.Code);

            await act.Should().ThrowAsync<BusinessRuleException>()
                .WithMessage("*fora de alcance*");

            // Uma tabela calada jamais apaga o que a ficha já sabia.
            vehicle.FipeValue.Should().Be(66_000m);
            vehicle.FipeSource.Should().Be(FipeSource.Manual);

            world.UnitOfWork.Verify(
                unit => unit.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ACarOutsideTheTable_IsToldApartFromAFailure()
        {
            var world = new World();
            var vehicle = world.GivenVehicle(fipeCode: "999999-9", yearFuel: "2014-5");

            world.TheCarIsOutsideTheTable();

            var act = () => world.Refresh(vehicle.Code);

            // Importado, muito antigo ou nunca precificado é fato final, e a frase diz isso
            // em vez de mandar tentar de novo.
            await act.Should().ThrowAsync<BusinessRuleException>()
                .WithMessage("*segue sem este modelo*");
        }

        private sealed class World
        {
            private readonly int tenantOfTheVehicle;
            private readonly List<Vehicle> yard = [];
            private FipeResult<FipeQuote> answer;
            private FipeResult<FipeYearOption> years;

            public World(int tenantOfTheVehicle = IdTenant)
            {
                this.tenantOfTheVehicle = tenantOfTheVehicle;

                answer = FipeResult<FipeQuote>.Found(FipeQuote.Create(
                    "004380-0", "2014-5", Setembro, 56_530.00m, 2014,
                    "GM - Chevrolet", "CRUZE LT 1.8 16V FlexPower 4p Aut."));

                years = FipeResult<FipeYearOption>.Found(new FipeYearOption("2014-5", "2014 Flex", 2014));

                Vehicles = new Mock<IVehicleRepository>();

                Vehicles.Setup(repository => repository.GetByCodeAsync(
                        IdTenant, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((int _, Guid code, CancellationToken _) =>
                        yard.FirstOrDefault(vehicle =>
                            vehicle.Code == code && vehicle.IdTenant == IdTenant));

                Audit = new Mock<IAuditLogRepository>();

                UnitOfWork = new Mock<IUnitOfWork>();
                UnitOfWork.SetupGet(unit => unit.VehicleRepository).Returns(Vehicles.Object);
                UnitOfWork.SetupGet(unit => unit.AuditLogRepository).Returns(Audit.Object);

                Quotes = new Mock<IFipeQuoteReader>();

                Quotes.Setup(reader => reader.GetCurrentAsync(
                        It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(() => answer);

                Quotes.Setup(reader => reader.ResolveYearFuelAsync(
                        It.IsAny<string>(), It.IsAny<short>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(() => years);

                var currentUser = new Mock<ICurrentUser>();
                currentUser.SetupGet(user => user.Id).Returns(9);
                currentUser.SetupGet(user => user.IdTenant).Returns(IdTenant);
                currentUser.SetupGet(user => user.Code).Returns(Guid.NewGuid());

                Handler = new RefreshVehicleFipeHandler(
                    UnitOfWork.Object, currentUser.Object, Quotes.Object);
            }

            public Mock<IUnitOfWork> UnitOfWork { get; }

            public Mock<IVehicleRepository> Vehicles { get; }

            public Mock<IAuditLogRepository> Audit { get; }

            public Mock<IFipeQuoteReader> Quotes { get; }

            private RefreshVehicleFipeHandler Handler { get; }

            public Vehicle GivenVehicle(string? fipeCode, string? yearFuel)
            {
                var vehicle = Vehicle.Create(
                    tenantOfTheVehicle, "MKT7H21", "9BWZZZ377VT004251",
                    "Chevrolet", "Cruze", 2014, 2013);

                vehicle.Id = 42;

                if (fipeCode is not null && yearFuel is not null)
                {
                    vehicle.SetFipeModel(fipeCode, yearFuel);
                }
                else if (fipeCode is not null)
                {
                    vehicle.SetFipe(null, null, fipeCode);
                }

                yard.Add(vehicle);

                return vehicle;
            }

            public Task<Application.Vehicles.DTOs.FipeReferenceDto> Refresh(Guid code) =>
                Handler.Handle(new RefreshVehicleFipeCommand(code), CancellationToken.None);

            public void TheTableIsOutOfReach() =>
                answer = FipeResult<FipeQuote>.Unavailable("A fonte está fora de alcance.");

            public void TheCarIsOutsideTheTable() =>
                answer = FipeResult<FipeQuote>.Missing("A tabela respondeu 404.");
        }
    }
}
