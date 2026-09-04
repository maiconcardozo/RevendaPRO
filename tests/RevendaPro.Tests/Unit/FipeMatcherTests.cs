using FluentAssertions;
using RevendaPro.Application.Fipe;
using RevendaPro.Domain.Entities;
using RevendaPro.Domain.Enums;
using RevendaPro.Domain.ValueObjects;

namespace RevendaPro.Tests.Unit
{
    /// <summary>
    /// O casador que transforma "Jeep / Renegade / 1.8 Longitude" numa linha da tabela.
    ///
    /// <i>"Dê a inteligência para tentar buscar o menor número de resultados possíveis, mas
    /// sempre busque e dê as opções."</i>
    ///
    /// <b>Todos os nomes deste arquivo saíram da tabela de verdade</b>, lidos em 4 de setembro de
    /// 2026 pela própria API do sistema. É o que faz este teste valer alguma coisa: um casador
    /// afinado contra nomes inventados acerta os nomes inventados.
    ///
    /// O que se prova aqui é a decisão 3 do V0 — cada sinal só <b>descarta</b>, e sobrando zero o
    /// casador volta um passo em vez de responder vazio.
    /// </summary>
    public class FipeMatcherTests
    {
        private const int IdTenant = 7;

        /// <summary>Nomes de verdade da Jeep, dos 32 que a tabela escreve com "Renegade".</summary>
        private static readonly FipeNamed[] Renegades =
        [
            new("1", "Renegade 1.8 4x2 Flex 16V Aut."),
            new("2", "Renegade 1.8 4x2 Flex 16V Mec."),
            new("3", "Renegade 75 Anos 1.8 4X2 Flex 16V Aut."),
            new("4", "Renegade 75 Anos 2.0 4X4 TB Diesel Aut."),
            new("5", "Renegade Altitude T270 1.3 TB Flex Aut."),
            new("6", "Renegade Custom 1.8 4x2 Flex 16V Mec."),
            new("7", "Renegade Custom 2.0 4x4 TB Diesel Aut."),
            new("8", "Renegade Lim. Edit. 1.8 4x2 Flex 16V Aut"),
            new("9", "Renegade Longitude 1.8 4x2 Flex 16V Aut."),
        ];

        /// <summary>As duas linhas que a tabela tem para o Gol 1.6 MSI, e três Golf.</summary>
        private static readonly FipeNamed[] Volkswagens =
        [
            new("8463", "Gol 1.6 MSI Flex 16V 5p Aut."),
            new("8324", "Gol 1.6 MSI Flex 8V 5p"),
            new("101", "Golf  BLACK EDITON 2.0 Mi T. Flex 8V Tip"),
            new("102", "Golf  TECH 1.6 Mi Total Flex 8V 4p"),
            new("103", "Golf 1.6 Mi Total Flex 8V 4p"),
        ];

        [Fact]
        public void TheVersion_TurnsThirtyTwoRenegadesIntoOne()
        {
            var jeep = Car("Jeep", "Renegade", "1.8 Longitude", TransmissionType.Automatic);

            var candidatos = FipeModelMatcher.Narrow(Renegades, jeep);

            // "1.8" e "Longitude" juntos existem numa linha só. É o caso em que o sistema tem o
            // direito de gravar sozinho, porque escolha nenhuma sobrou para fazer.
            candidatos.Should().ContainSingle()
                .Which.Name.Should().Be("Renegade Longitude 1.8 4x2 Flex 16V Aut.");
        }

        [Fact]
        public void AStickShift_IsRecognizedByWhatTheTableLeavesUnwritten()
        {
            var gol = Car("Volkswagen", "Gol", "1.6 MSI", TransmissionType.Manual);

            var candidatos = FipeModelMatcher.Narrow(Volkswagens, gol);

            // A tabela marca o automático e deixa o manual sem marca: das duas linhas de Gol 1.6
            // MSI, uma diz "Aut." e a outra diz nada. Procurar a palavra "Mec." aqui acharia nada
            // e separaria nenhuma das duas.
            candidatos.Should().ContainSingle()
                .Which.Name.Should().Be("Gol 1.6 MSI Flex 8V 5p");
        }

        [Fact]
        public void AnAutomatic_TakesTheOtherLineOfTheSameCar()
        {
            var gol = Car("Volkswagen", "Gol", "1.6 MSI", TransmissionType.Automatic);

            var candidatos = FipeModelMatcher.Narrow(Volkswagens, gol);

            candidatos.Should().ContainSingle()
                .Which.Name.Should().Be("Gol 1.6 MSI Flex 16V 5p Aut.");
        }

        [Fact]
        public void TheNameIsAWholeWord_SoAGolIsNeverAGolf()
        {
            var gol = Car("Volkswagen", "Gol", version: null, TransmissionType.Manual);

            var candidatos = FipeModelMatcher.Narrow(Volkswagens, gol);

            // Sem esta regra, "gol" casaria com os três Golf e o carro entraria na lista errada —
            // e um Golf custa quase o dobro de um Gol.
            candidatos.Should().OnlyContain(model => model.Name.StartsWith("Gol ", StringComparison.Ordinal));
        }

        [Fact]
        public void AVersionTheTableNeverWrote_StepsBackInsteadOfAnsweringNothing()
        {
            var jeep = Car("Jeep", "Renegade", "Trailhawk Serra Gaúcha", TransmissionType.Automatic);

            var candidatos = FipeModelMatcher.Narrow(Renegades, jeep);

            // Nenhum termo desta versão existe na tabela. Responder vazio mandaria a pessoa de
            // volta para a lista de cem; oferecer os automáticos que sobraram é uma resposta.
            candidatos.Should().NotBeEmpty();
            candidatos.Should().OnlyContain(model => model.Name.Contains("Aut", StringComparison.Ordinal));
        }

        [Fact]
        public void AModelTheTableNeverPriced_AnswersNothing()
        {
            var jeep = Car("Jeep", "Comanche", "4.0", TransmissionType.Manual);

            var candidatos = FipeModelMatcher.Narrow(Renegades, jeep);

            // Vazio aqui é honesto: a tabela segue sem este carro, e inventar o Renegade mais
            // parecido poria um preço de outro carro na ficha.
            candidatos.Should().BeEmpty();
        }

        [Theory]
        [InlineData("Chevrolet", "GM - Chevrolet")]
        [InlineData("Volkswagen", "VW - VolksWagen")]
        [InlineData("Jeep", "Jeep")]
        public void TheBrandOfTheTable_IsFoundByHowItWritesItself(string doVeiculo, string daTabela)
        {
            var marcas = new FipeNamed[]
            {
                new("22", "Fiat"),
                new("23", "GM - Chevrolet"),
                new("29", "Jeep"),
                new("59", "VW - VolksWagen"),
            };

            var marca = FipeModelMatcher.FindBrand(marcas, doVeiculo);

            marca!.Name.Should().Be(daTabela);
        }

        [Fact]
        public void ABrandNobodyPrices_AnswersNothing()
        {
            var marcas = new FipeNamed[] { new("22", "Fiat"), new("29", "Jeep") };

            FipeModelMatcher.FindBrand(marcas, "Lada").Should().BeNull();
        }

        [Fact]
        public void TheYearOfTheCar_KeepsOnlyTheRowsThatPriceIt()
        {
            var anos = new FipeYearOption[]
            {
                new("2019-1", "2019 Gasolina", 2019),
                new("2020-1", "2020 Gasolina", 2020),
                new("2020-3", "2020 Flex", 2020),
                new("2021-3", "2021 Flex", 2021),
            };

            var doAno = FipeModelMatcher.YearsOf(anos, 2020);

            // Duas linhas para o mesmo ano é o caso que existe de verdade — flex e gasolina, com
            // preços diferentes —, e por isso o casador entrega as duas em vez de escolher.
            doAno.Should().HaveCount(2);
            doAno.Should().OnlyContain(option => option.ModelYear == 2020);
        }

        private static Vehicle Car(
            string brand,
            string model,
            string? version,
            TransmissionType transmission)
        {
            var vehicle = Vehicle.Create(
                IdTenant, "ABC1D23", "9BWZZZ377VT004251", brand, model, 2020, 2019);

            vehicle.SetDetails(version, "Branco", FuelType.Flex, transmission, null, null);

            return vehicle;
        }
    }
}
