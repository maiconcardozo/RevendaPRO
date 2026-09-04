using FluentAssertions;
using MediatR;
using Moq;
using RevendaPro.Application.Vehicles.Commands;
using RevendaPro.Application.Vehicles.DTOs;
using RevendaPro.Application.Vehicles.Handlers;
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
    /// O botão que procura o modelo na tabela em vez de mandar procurar.
    ///
    /// <i>"Dê a inteligência para tentar buscar o menor número de resultados possíveis, mas
    /// sempre busque e dê as opções."</i>
    ///
    /// O que se prova aqui é a decisão 2 do V0: sobrando <b>um</b> candidato com <b>um</b> ano, o
    /// sistema grava porque escolha nenhuma restou para fazer; sobrando qualquer outro número, a
    /// escolha volta para quem conhece o carro — e nada é escrito.
    /// </summary>
    public class FipeMatchHandlerTests
    {
        private const int IdTenant = 7;
        private const int OtherTenant = 8;
        private static readonly DateOnly Setembro = new(2026, 9, 1);

        [Fact]
        public async Task OneCandidateWithOneYear_IsWrittenWithoutAsking()
        {
            var world = new World();
            var vehicle = world.GivenCar("Jeep", "Renegade", "1.8 Longitude");

            world.TheTableAnswers(
                ("9", "Renegade Longitude 1.8 4x2 Flex 16V Aut.", new[] { ("2020-5", 2020) }));

            var match = await world.Match(vehicle.Code);

            match.Applied.Should().NotBeNull();
            match.Candidates.Should().BeEmpty();

            // A escrita sai pela mesma porta que a pessoa usaria, e não por um caminho paralelo:
            // é o que faz o código gravado, a cotação guardada e a auditoria saírem iguais.
            world.Mediator.Verify(
                mediator => mediator.Send(
                    It.Is<SetVehicleFipeModelCommand>(command =>
                        command.Code == vehicle.Code
                        && command.ModelCode == "9"
                        && command.YearFuel == "2020-5"),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task TwoTrimsOfTheSameCar_GoBackAsAQuestion_AndNothingIsWritten()
        {
            var world = new World();
            var vehicle = world.GivenCar("Chevrolet", "Onix", "1.4 LT");

            world.TheTableAnswers(
                ("1", "ONIX HATCH LT 1.4 8V FlexPower 5p Mec.", new[] { ("2020-1", 2020) }),
                ("2", "ONIX HATCH LTZ 1.4 8V FlexPower 5p Mec.", new[] { ("2020-1", 2020) }));

            var match = await world.Match(vehicle.Code);

            // Duas versões do mesmo carro são dois preços. Escolher por conta própria aqui poria
            // o preço de outro carro na ficha.
            match.Applied.Should().BeNull();
            match.Candidates.Should().HaveCount(2);

            world.Mediator.Verify(
                mediator => mediator.Send(
                    It.IsAny<SetVehicleFipeModelCommand>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task AYearTheTableNeverPriced_DropsTheCandidate()
        {
            var world = new World();
            var vehicle = world.GivenCar("Chevrolet", "Onix", "1.4 LT");

            world.TheTableAnswers(
                ("1", "ONIX HATCH LT 1.4 8V FlexPower 5p Mec.", new[] { ("2020-1", 2020) }),
                ("2", "ONIX HATCH LTZ 1.4 8V FlexPower 5p Mec.", new[] { ("2013-1", 2013) }));

            var match = await world.Match(vehicle.Code);

            // O ano é o descarte mais forte que existe: uma versão que a tabela jamais
            // precificou em 2020 não pode ser um carro 2020. Sobrando um, ele é gravado.
            match.Applied.Should().NotBeNull();

            world.Mediator.Verify(
                mediator => mediator.Send(
                    It.Is<SetVehicleFipeModelCommand>(command => command.ModelCode == "1"),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task TwoFuelsOfTheSameYear_GoBackAsAQuestion()
        {
            var world = new World();
            var vehicle = world.GivenCar("Jeep", "Renegade", "1.8 Longitude");

            world.TheTableAnswers(
                ("9", "Renegade Longitude 1.8 4x2 Flex 16V Aut.",
                    new[] { ("2020-1", 2020), ("2020-5", 2020) }));

            var match = await world.Match(vehicle.Code);

            // Um modelo só, e ainda assim dois preços: o mesmo ano existe como flex e como
            // gasolina. É pergunta, e não palpite.
            match.Applied.Should().BeNull();
            match.Candidates.Should().ContainSingle()
                .Which.Years.Should().HaveCount(2);
        }

        [Fact]
        public async Task ABrandTheTableNeverPriced_AnswersNothing_AndAsksNoModels()
        {
            var world = new World();
            var vehicle = world.GivenCar("Lada", "Niva", "1.6");

            world.TheTableAnswers(("1", "Renegade Longitude 1.8", new[] { ("2020-5", 2020) }));

            var match = await world.Match(vehicle.Code);

            match.Applied.Should().BeNull();
            match.Candidates.Should().BeEmpty();

            world.Catalog.Verify(
                catalog => catalog.ListModelsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task ACarOfAnotherDealership_IsRefused_AndTheSourceIsNeverTouched()
        {
            var world = new World(tenantOfTheCar: OtherTenant);
            var vehicle = world.GivenCar("Jeep", "Renegade", "1.8 Longitude");

            var act = () => world.Match(vehicle.Code);

            await act.Should().ThrowAsync<NotFoundException>();

            // A empresa é conferida antes de qualquer ida à fonte (RNF-04).
            world.Catalog.Verify(
                catalog => catalog.ListBrandsAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task WithTheTableOutOfReach_TheAnswerIsASentence_AndNeverARawFailure()
        {
            var world = new World();
            var vehicle = world.GivenCar("Jeep", "Renegade", "1.8 Longitude");

            world.TheListsAreOutOfReach();

            var act = () => world.Match(vehicle.Code);

            await act.Should().ThrowAsync<BusinessRuleException>().WithMessage("*fora de alcance*");
        }

        [Fact]
        public async Task WhenTheBestNamesLackTheYear_TheSearchGoesDownATier()
        {
            var world = new World();
            var gol = world.GivenCar("Volkswagen", "Gol", "1.6 MSI", 2015, TransmissionType.Manual);

            // Nomes e anos de verdade, lidos da tabela em 4 de setembro de 2026. A palavra "MSI"
            // acerta em cheio duas linhas — e as duas só existem de 2019 em diante.
            world.TheTableAnswers(
                ("8324", "Gol 1.6 MSI Flex 8V 5p",
                    [("2019-1", 2019), ("2020-1", 2020), ("2021-1", 2021), ("2022-1", 2022)]),
                ("8463", "Gol 1.6 MSI Flex 16V 5p Aut.",
                    [("2019-1", 2019), ("2020-1", 2020)]),
                ("7011", "Gol Trendline 1.6 T.Flex 8V 5p",
                    [("2015-1", 2015), ("2016-1", 2016), ("2017-1", 2017), ("2018-1", 2018)]),
                ("7012", "Gol Comfortline 1.6 T. Flex 8V 5p",
                    [("2015-1", 2015), ("2016-1", 2016)]));

            var match = await world.Match(gol.Code);

            // Num Gol 2015, a camada do "MSI" cede: naquela geração a tabela escreve o mesmo
            // motor como acabamento — Trendline, Comfortline —, e a palavra MSI só aparece de
            // 2019 em diante. Parar na melhor camada poria na ficha o preço de um carro quatro
            // anos mais novo.
            match.Applied.Should().BeNull();
            match.Candidates.Should().HaveCount(2);
            match.Candidates.Should().OnlyContain(candidate =>
                candidate.Name.Contains("Trendline", StringComparison.Ordinal)
                || candidate.Name.Contains("Comfortline", StringComparison.Ordinal));
        }

        [Fact]
        public async Task TheYearIsARequirement_AndNeverATiebreaker()
        {
            var world = new World();
            var gol = world.GivenCar("Volkswagen", "Gol", "1.6 MSI", 2015, TransmissionType.Manual);

            world.TheTableAnswers(
                ("8324", "Gol 1.6 MSI Flex 8V 5p", [("2019-1", 2019), ("2020-1", 2020)]),
                ("7011", "Gol Trendline 1.6 T.Flex 8V 5p", [("2015-1", 2015)]));

            var match = await world.Match(gol.Code);

            // Um só com o ano, e um ano só: escolha nenhuma sobrou para fazer.
            match.Applied.Should().NotBeNull();

            world.Mediator.Verify(
                mediator => mediator.Send(
                    It.Is<SetVehicleFipeModelCommand>(command => command.ModelCode == "7011"),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task WithNoTierAnsweringForTheYear_TheBestNamesComeBackWithoutYears()
        {
            var world = new World();
            var gol = world.GivenCar("Volkswagen", "Gol", "1.6 MSI", 2015, TransmissionType.Manual);

            world.TheTableAnswers(
                ("8324", "Gol 1.6 MSI Flex 8V 5p", [("2019-1", 2019)]),
                ("8463", "Gol 1.6 MSI Flex 16V 5p Aut.", [("2020-1", 2020)]));

            var match = await world.Match(gol.Code);

            // Nenhuma camada responde por 2015. Volta a melhor delas, e sem anos: é o sinal de
            // que a tela precisa dizer isso — e jamais oferecer o preço de outra geração como se
            // fosse deste carro. A melhor é uma só porque o câmbio separa as duas: este Gol é
            // manual, e a tabela marca o automático.
            match.Applied.Should().BeNull();
            match.Candidates.Should().ContainSingle()
                .Which.Name.Should().Be("Gol 1.6 MSI Flex 8V 5p");
            match.Candidates.Should().OnlyContain(candidate => candidate.Years.Count == 0);
        }

        [Fact]
        public async Task TheBudget_StopsASearchFromBecomingHundredsOfCalls()
        {
            var world = new World();
            var vehicle = world.GivenCar("Jeep", "Renegade", version: null);

            // Quarenta versões, e nenhuma do ano do carro: sem teto, a busca faria quarenta
            // perguntas numa fonte de terceiros com limite de uso.
            world.TheTableAnswers([.. Enumerable.Range(1, 40).Select(number =>
                (number.ToString(), $"Renegade versao {number}",
                    new[] { ("2005-1", 2005) }))]);

            await world.Match(vehicle.Code);

            world.Catalog.Verify(
                catalog => catalog.ListModelYearsAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Exactly(30));
        }

        private sealed class World
        {
            private readonly int tenantOfTheCar;
            private readonly List<Vehicle> yard = [];
            private readonly Dictionary<string, IReadOnlyList<FipeYearOption>> yearsByModel = [];
            private FipeResult<IReadOnlyList<FipeNamed>> brands;
            private FipeResult<IReadOnlyList<FipeNamed>> models;

            public World(int tenantOfTheCar = IdTenant)
            {
                this.tenantOfTheCar = tenantOfTheCar;

                brands = FipeResult<IReadOnlyList<FipeNamed>>.Found(
                    [
                        new FipeNamed("29", "Jeep"),
                        new FipeNamed("23", "GM - Chevrolet"),
                        new FipeNamed("59", "VW - VolksWagen"),
                    ]);

                models = FipeResult<IReadOnlyList<FipeNamed>>.Found([]);

                var vehicles = new Mock<IVehicleRepository>();
                vehicles.Setup(repository => repository.GetByCodeAsync(
                        IdTenant, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((int _, Guid code, CancellationToken _) =>
                        yard.FirstOrDefault(v => v.Code == code && v.IdTenant == IdTenant));

                var unitOfWork = new Mock<IUnitOfWork>();
                unitOfWork.SetupGet(unit => unit.VehicleRepository).Returns(vehicles.Object);

                Catalog = new Mock<IFipeCatalog>();

                Catalog.Setup(catalog => catalog.ListBrandsAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(() => brands);

                Catalog.Setup(catalog => catalog.ListModelsAsync(
                        It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(() => models);

                Catalog.Setup(catalog => catalog.ListModelYearsAsync(
                        It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((string _, string model, CancellationToken _) =>
                        FipeResult<IReadOnlyList<FipeYearOption>>.Found(
                            yearsByModel.TryGetValue(model, out var found) ? found : []));

                Mediator = new Mock<IMediator>();

                Mediator.Setup(mediator => mediator.Send(
                        It.IsAny<SetVehicleFipeModelCommand>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new FipeReferenceDto(
                        74_969.00m, Setembro, "015123-4", "2020-5", FipeSource.Automatic,
                        "Jeep", "Renegade Longitude 1.8 4x2 Flex 16V Aut.", null));

                var currentUser = new Mock<ICurrentUser>();
                currentUser.SetupGet(user => user.IdTenant).Returns(IdTenant);
                currentUser.SetupGet(user => user.Code).Returns(Guid.NewGuid());

                Handler = new MatchVehicleFipeModelHandler(
                    unitOfWork.Object, currentUser.Object, Catalog.Object, Mediator.Object);
            }

            public Mock<IFipeCatalog> Catalog { get; }

            public Mock<IMediator> Mediator { get; }

            private MatchVehicleFipeModelHandler Handler { get; }

            public Vehicle GivenCar(
                string brand,
                string model,
                string? version,
                short modelYear = 2020,
                TransmissionType transmission = TransmissionType.Automatic)
            {
                var vehicle = Vehicle.Create(
                    IdTenant, "ABC1D23", "9BWZZZ377VT004251", brand, model,
                    modelYear, (short)(modelYear - 1));

                vehicle.SetDetails(version, "Branco", FuelType.Flex, transmission, null, null);
                vehicle.Id = 42;

                if (tenantOfTheCar == IdTenant)
                {
                    yard.Add(vehicle);
                }

                return vehicle;
            }

            /// <summary>Diz o que a tabela responde: os modelos, e os anos de cada um.</summary>
            public void TheTableAnswers(
                params (string Code, string Name, (string YearFuel, int Year)[] Years)[] lines)
            {
                models = FipeResult<IReadOnlyList<FipeNamed>>.Found(
                    [.. lines.Select(line => new FipeNamed(line.Code, line.Name))]);

                foreach (var line in lines)
                {
                    yearsByModel[line.Code] =
                    [
                        .. line.Years.Select(year =>
                            new FipeYearOption(year.YearFuel, $"{year.Year} Flex", (short)year.Year)),
                    ];
                }
            }

            public void TheListsAreOutOfReach() =>
                brands = FipeResult<IReadOnlyList<FipeNamed>>.Unavailable("a fonte ficou muda");

            public Task<FipeMatchDto> Match(Guid code) =>
                Handler.Handle(new MatchVehicleFipeModelCommand(code), CancellationToken.None);
        }
    }
}
