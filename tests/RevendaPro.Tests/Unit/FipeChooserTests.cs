using FluentAssertions;
using Moq;
using RevendaPro.Application.Fipe;
using RevendaPro.Application.Vehicles.Commands;
using RevendaPro.Application.Vehicles.DTOs;
using RevendaPro.Application.Vehicles.Handlers;
using RevendaPro.Application.Vehicles.Queries;
using RevendaPro.Domain.Entities;
using RevendaPro.Domain.Enums;
using RevendaPro.Domain.Interfaces;
using RevendaPro.Domain.Interfaces.Reference;
using RevendaPro.Domain.Interfaces.Repositories;
using RevendaPro.Domain.Interfaces.Security;
using RevendaPro.Domain.ValueObjects;
using RevendaPro.Shared.Exceptions;

namespace RevendaPro.Tests.Unit
{
    /// <summary>
    /// As três escolhas que dão um código da tabela ao carro que ainda não tem nenhum.
    ///
    /// Ninguém decora código de FIPE, e o M6 guardou esse campo justamente para este marco.
    /// O que se prova aqui é o critério do V4: marca, modelo e ano bastam — e da segunda vez
    /// em diante a consulta é direta, porque o código ficou gravado.
    /// </summary>
    public class FipeChooserTests
    {
        private const int IdTenant = 7;
        private const int OtherTenant = 8;

        private static readonly DateOnly Setembro = new(2026, 9, 1);

        [Fact]
        public async Task ThreeChoices_GiveTheCarACodeAndAValue()
        {
            var world = new World();
            var vehicle = world.GivenVehicleWithoutACode();

            var answer = await world.Choose(vehicle.Code, "21", "7965", "2020-5");

            answer.Value.Should().Be(51_757.00m);
            answer.Source.Should().Be(FipeSource.Automatic);

            // O código é o que transforma toda leitura seguinte numa chamada direta. Ele vem
            // da resposta, e jamais de uma escolha da tela: é o que a tabela imprimiu.
            vehicle.FipeCode.Should().Be("001494-0");
            vehicle.FipeYearFuel.Should().Be("2020-5");
            vehicle.FipeValue.Should().Be(51_757.00m);
            vehicle.FipeReferenceDate.Should().Be(Setembro);
            vehicle.FipeSource.Should().Be(FipeSource.Automatic);
        }

        [Fact]
        public async Task TheChosenModel_IsAskedWithTheTablePinned()
        {
            var world = new World();
            var vehicle = world.GivenVehicleWithoutACode();

            await world.Choose(vehicle.Code, "21", "7965", "2020-5");

            // As listas que levaram até aqui vão sem mês fixado, porque respondem nomes. Esta
            // responde dinheiro, e a mesma fonte já devolveu dois meses diferentes para o
            // mesmo carro dentro de um minuto.
            world.Catalog.Verify(
                catalog => catalog.GetPriceOfModelAsync(
                    "21", "7965", "2020-5", 337, It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task TheQuoteGoesInThroughTheSameDoor_SoTheNextCarOfTheModelCostsNothing()
        {
            var world = new World();
            var vehicle = world.GivenVehicleWithoutACode();

            await world.Choose(vehicle.Code, "21", "7965", "2020-5");

            world.Quotes.Verify(
                reader => reader.KeepAsync(
                    It.Is<FipePrice>(price => price.FipeCode == "001494-0"),
                    It.IsAny<CancellationToken>()),
                Times.Once);

            world.UnitOfWork.Verify(
                unit => unit.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task TheChoiceIsRecorded()
        {
            var world = new World();
            var vehicle = world.GivenVehicleWithoutACode();

            await world.Choose(vehicle.Code, "21", "7965", "2020-5");

            world.Audit.Verify(
                repository => repository.Add(It.Is<AuditLog>(log =>
                    log.EntityName == nameof(Vehicle)
                    && log.RecordCode == vehicle.Code
                    && log.Action == AuditAction.Update)),
                Times.Once);
        }

        [Fact]
        public async Task AVehicleOfAnotherDealership_IsRefused_AndNothingIsAsked()
        {
            var world = new World(tenantOfTheVehicle: OtherTenant);
            var vehicle = world.GivenVehicleWithoutACode();

            var act = () => world.Choose(vehicle.Code, "21", "7965", "2020-5");

            await act.Should().ThrowAsync<NotFoundException>();

            // A empresa é conferida antes de qualquer ida à fonte (RNF-04).
            world.Catalog.Verify(
                catalog => catalog.GetPriceOfModelAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task WithTheTableOutOfReach_NothingIsWritten()
        {
            var world = new World();
            var vehicle = world.GivenVehicleWithoutACode();

            world.TheTableIsOutOfReach();

            var act = () => world.Choose(vehicle.Code, "21", "7965", "2020-5");

            await act.Should().ThrowAsync<BusinessRuleException>()
                .WithMessage("*fora de alcance*");

            vehicle.FipeCode.Should().BeNull();

            world.UnitOfWork.Verify(
                unit => unit.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task TheBrandsAreListedForTheScreen()
        {
            var world = new World();

            var brands = await world.Brands();

            brands.Should().HaveCount(2);
            brands[0].Code.Should().Be("21");
            brands[0].Name.Should().Be("Fiat");
        }

        [Fact]
        public async Task ASourceThatStaysQuiet_RefusesTheListing_AndNeverThrowsRaw()
        {
            var world = new World();
            world.TheListsAreOutOfReach();

            var act = () => world.Brands();

            // Uma tela pedindo marcas com a fonte fora do ar recebe uma frase, e jamais uma
            // falha inesperada: a tabela de referência jamais derruba a operação.
            await act.Should().ThrowAsync<BusinessRuleException>()
                .WithMessage("*fora de alcance*");
        }

        private sealed class World
        {
            private readonly int tenantOfTheVehicle;
            private readonly List<Vehicle> yard = [];
            private FipeResult<FipePrice> priced;
            private FipeResult<FipeReference> tables;
            private FipeResult<IReadOnlyList<FipeNamed>> brands;

            public World(int tenantOfTheVehicle = IdTenant)
            {
                this.tenantOfTheVehicle = tenantOfTheVehicle;

                tables = FipeResult<FipeReference>.Found(new FipeReference(337, Setembro));

                priced = FipeResult<FipePrice>.Found(new FipePrice(
                    "001494-0", "2020-5", Setembro, 51_757.00m,
                    "Fiat", "ARGO DRIVE 1.0 6V Flex", 2020, "Flex"));

                brands = FipeResult<IReadOnlyList<FipeNamed>>.Found(
                    [new FipeNamed("21", "Fiat"), new FipeNamed("23", "GM - Chevrolet")]);

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

                Catalog = new Mock<IFipeCatalog>();

                Catalog.Setup(catalog => catalog.ListBrandsAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(() => brands);

                Catalog.Setup(catalog => catalog.GetPriceOfModelAsync(
                        It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                        It.IsAny<int>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(() => priced);

                Quotes = new Mock<IFipeQuoteReader>();

                Quotes.Setup(reader => reader.PublishedTableAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(() => tables);

                Quotes.Setup(reader => reader.KeepAsync(
                        It.IsAny<FipePrice>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((FipePrice price, CancellationToken _) => FipeQuote.Create(price));

                var currentUser = new Mock<ICurrentUser>();
                currentUser.SetupGet(user => user.Id).Returns(9);
                currentUser.SetupGet(user => user.IdTenant).Returns(IdTenant);
                currentUser.SetupGet(user => user.Code).Returns(Guid.NewGuid());

                Chooser = new SetVehicleFipeModelHandler(
                    UnitOfWork.Object, currentUser.Object, Catalog.Object, Quotes.Object);

                Listing = new ListFipeBrandsHandler(Catalog.Object);
            }

            public Mock<IUnitOfWork> UnitOfWork { get; }

            public Mock<IVehicleRepository> Vehicles { get; }

            public Mock<IAuditLogRepository> Audit { get; }

            public Mock<IFipeCatalog> Catalog { get; }

            public Mock<IFipeQuoteReader> Quotes { get; }

            private SetVehicleFipeModelHandler Chooser { get; }

            private ListFipeBrandsHandler Listing { get; }

            public Vehicle GivenVehicleWithoutACode()
            {
                var vehicle = Vehicle.Create(
                    tenantOfTheVehicle, "XYZ9A88", "9BWZZZ377VT004299",
                    "Fiat", "Argo", 2020, 2019);

                vehicle.Id = 43;
                yard.Add(vehicle);

                return vehicle;
            }

            public Task<FipeReferenceDto> Choose(
                Guid code, string brand, string model, string yearFuel) =>
                Chooser.Handle(
                    new SetVehicleFipeModelCommand(code, brand, model, yearFuel),
                    CancellationToken.None);

            public Task<IReadOnlyList<FipeOptionDto>> Brands() =>
                Listing.Handle(new ListFipeBrandsQuery(), CancellationToken.None);

            public void TheTableIsOutOfReach() =>
                tables = FipeResult<FipeReference>.Unavailable("A fonte está fora de alcance.");

            public void TheListsAreOutOfReach() =>
                brands = FipeResult<IReadOnlyList<FipeNamed>>.Unavailable("A fonte está fora de alcance.");
        }
    }
}
