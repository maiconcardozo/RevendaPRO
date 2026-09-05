using FluentAssertions;
using MediatR;
using Moq;
using RevendaPro.Application.Fipe;
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
    /// O que se prova aqui é a decisão 1 do V0 do <b>M16</b>: um candidato ou vinte, a resposta é
    /// a lista, e <b>nada</b> é escrito. Até o M15 sobrar um com um ano só bastava para o sistema
    /// gravar sozinho; o uso mostrou que sobrar um prova que o casador eliminou os outros, e
    /// jamais que ele acertou este.
    /// </summary>
    public class FipeMatchHandlerTests
    {
        private const int IdTenant = 7;
        private const int OtherTenant = 8;
        private static readonly DateOnly Setembro = new(2026, 9, 1);

        [Fact]
        public async Task OneCandidateWithOneYear_StillGoesBackAsAQuestion_AndNothingIsWritten()
        {
            var world = new World();
            var vehicle = world.GivenCar("Jeep", "Renegade", "1.8 Longitude");

            world.TheTableAnswers(
                ("9", "Renegade Longitude 1.8 4x2 Flex 16V Aut.", new[] { ("2020-5", 2020) }));

            var match = await world.Match(vehicle.Code);

            // O caso que o M15 gravava sozinho. Ele volta como lista de um — com preço, código,
            // ano e a nota cheia —, e o pop-up abre com ele para a pessoa ver o que vai gravar.
            match.Candidates.Should().ContainSingle();
            match.Candidates[0].Name.Should().Be("Renegade Longitude 1.8 4x2 Flex 16V Aut.");
            match.Candidates[0].Accuracy.Should().Be(100);
            match.Candidates[0].Recommended.Should().BeTrue();
            match.Candidates[0].Value.Should().Be(74_969.00m);
            match.Candidates[0].FipeCode.Should().Be("015123-4");

            world.NothingWasWritten();
        }

        [Fact]
        public async Task TheNoteHighlightsOneCandidate_AndWritesNothingForIt()
        {
            var world = new World();
            var vehicle = world.GivenCar("Jeep", "Renegade", "Longitude", 2020, TransmissionType.Manual);

            // Duas linhas de verdade da tabela, e o carro é um Longitude manual e flex. A
            // primeira acerta o nome da versão; a segunda acerta câmbio e combustível — e as
            // duas empatam na camada, porque a camada conta ponto e a nota conta fração.
            world.TheTableAnswers(
                ("7", "Renegade Longitude 2.0 4x4 TB Diesel Aut.", new[] { ("2020-3", 2020) }),
                ("2", "Renegade 1.8 4x2 Flex 16V Mec.", new[] { ("2020-1", 2020) }));

            var match = await world.Match(vehicle.Code);

            // A palavra que nomeia a versão vale mais do que os dois sinais que confirmam sem
            // distinguir: o destaque vai para ela, e a lista chega ordenada por isso. Mesmo
            // assim, gravar continua sendo um clique da pessoa.
            match.Candidates.Should().HaveCount(2);
            match.Candidates[0].Name.Should().Contain("Longitude");
            match.Candidates[0].Recommended.Should().BeTrue();
            match.Candidates[1].Recommended.Should().BeFalse();
            match.Candidates[0].Accuracy.Should().BeGreaterThan(match.Candidates[1].Accuracy);

            world.NothingWasWritten();
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
            match.Candidates.Should().HaveCount(2);

            world.NothingWasWritten();
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
            // precificou em 2020 não pode ser um carro 2020. Sobrando um, ele volta sozinho na
            // lista — e a lista de um abre o mesmo pop-up que a de vinte.
            match.Candidates.Should().ContainSingle()
                .Which.ModelCode.Should().Be("1");

            world.NothingWasWritten();
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

            // A camada do "MSI" cede para a que a tabela precifica em 2015, e o Trendline volta
            // sozinho — para a pessoa ver, num carro cujo nome cadastrado sequer aparece na
            // linha que a tabela tem para ele. É exatamente o caso em que confirmar vale mais.
            match.Candidates.Should().ContainSingle()
                .Which.ModelCode.Should().Be("7011");

            world.NothingWasWritten();
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

        [Fact]
        public async Task CandidatesThatTieOnTheNote_ComeBackWithNoRecommendationAtAll()
        {
            var world = new World();
            var vehicle = world.GivenCar("Chevrolet", "Onix", "1.4 LT", 2020, TransmissionType.Manual);

            world.TheTableAnswers(
                ("1", "ONIX HATCH LT 1.4 8V FlexPower 5p Mec.", new[] { ("2020-1", 2020) }),
                ("2", "ONIX HATCH LTZ 1.4 8V FlexPower 5p Mec.", new[] { ("2020-1", 2020) }));

            var match = await world.Match(vehicle.Code);

            // Os dois nomes conferem os mesmos sinais deste carro. Destacar qualquer um seria
            // escolher no lugar de quem conhece o carro — e é justamente onde os dois preços
            // costumam ser bem diferentes. Onde o sistema empata, ele pergunta.
            match.Candidates.Should().HaveCount(2);
            match.Candidates.Should().OnlyContain(candidate => candidate.Recommended == false);
            match.Candidates.Select(candidate => candidate.Accuracy).Distinct()
                .Should().ContainSingle();
        }

        [Fact]
        public async Task ACarCadastradoWithNoVersion_PutsTheWholeListAtHalfTheMeter()
        {
            var world = new World();
            var vehicle = world.GivenCar("Volkswagen", "Gol", version: null, 2020, TransmissionType.Manual);

            world.TheTableAnswers(
                ("1", "Gol 1.0 Flex 8V 5p", new[] { ("2020-1", 2020) }),
                ("2", "Gol 1.6 Flex 8V 5p", new[] { ("2020-1", 2020) }),
                ("3", "Gol Track 1.0 Flex 12V 5p", new[] { ("2020-1", 2020) }));

            var match = await world.Match(vehicle.Code);

            // O carro cadastrado com pressa é o caso mais comum do pátio, e o medidor precisa
            // dizer a verdade sobre ele: metade deste carro segue por conferir, os três empatam
            // nessa metade, e recomendado nenhum aparece.
            match.Candidates.Should().HaveCount(3);
            match.Candidates.Should().OnlyContain(candidate => candidate.Accuracy == 50);
            match.Candidates.Should().OnlyContain(candidate => candidate.Recommended == false);
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

                Vehicles = vehicles;
                UnitOfWork = new Mock<IUnitOfWork>();
                UnitOfWork.SetupGet(unit => unit.VehicleRepository).Returns(vehicles.Object);

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

                var currentUser = new Mock<ICurrentUser>();
                currentUser.SetupGet(user => user.IdTenant).Returns(IdTenant);
                currentUser.SetupGet(user => user.Code).Returns(Guid.NewGuid());

                Quotes = new Mock<IFipeQuoteReader>();

                Quotes.Setup(reader => reader.PublishedTableAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(FipeResult<FipeReference>.Found(new FipeReference(337, Setembro)));

                // O preço de cada candidato, para o modal mostrar ao lado do nome.
                Catalog.Setup(catalog => catalog.GetPriceOfModelAsync(
                        It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                        It.IsAny<int>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(FipeResult<FipePrice>.Found(new FipePrice(
                        "015123-4", "2020-5", Setembro, 74_969.00m,
                        "Jeep", "Renegade Longitude 1.8 4x2 Flex 16V Aut.", 2020, "Flex")));

                Handler = new MatchVehicleFipeModelHandler(
                    UnitOfWork.Object, currentUser.Object, Catalog.Object, Quotes.Object);
            }

            public Mock<IFipeCatalog> Catalog { get; }

            public Mock<IFipeQuoteReader> Quotes { get; }

            public Mock<IUnitOfWork> UnitOfWork { get; }

            public Mock<IVehicleRepository> Vehicles { get; }

            private MatchVehicleFipeModelHandler Handler { get; }

            /// <summary>
            /// A prova de que a busca leu, e escreveu nada.
            ///
            /// É a frase inteira do M16 dita em teste: o pop-up abre com o que a fonte respondeu,
            /// e a ficha do carro só muda quando a pessoa aperta o botão que grava. Sem gravação
            /// automática, este handler perdeu a única escrita que tinha — e a garantia disso
            /// mora aqui, e jamais na leitura do código.
            /// </summary>
            public void NothingWasWritten()
            {
                Vehicles.Verify(
                    repository => repository.Update(It.IsAny<Vehicle>()), Times.Never);

                UnitOfWork.Verify(
                    unit => unit.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
            }

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
