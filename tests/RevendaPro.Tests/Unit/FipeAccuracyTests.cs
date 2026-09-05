using FluentAssertions;
using RevendaPro.Application.Fipe;
using RevendaPro.Domain.Entities;
using RevendaPro.Domain.Enums;

namespace RevendaPro.Tests.Unit
{
    /// <summary>
    /// O medidor de acurácia: o quanto de um carro um nome da tabela responde.
    ///
    /// <i>"Caso consiga, de inteligência para ele deixar em destaque o recomendado, como se
    /// fosse um medidor de acuracidade — mas quem seleciona é o usuário 100% dos casos."</i>
    ///
    /// O que se prova aqui é a decisão 2 do V0 do M16: a nota mede <b>o quanto do carro foi
    /// conferido</b>, e jamais o quanto do que foi digitado bateu. É a diferença entre um
    /// medidor honesto e um que marca 100% em vinte candidatos ao mesmo tempo.
    ///
    /// Os nomes saíram da tabela de verdade, lidos em 4 de setembro de 2026.
    /// </summary>
    public class FipeAccuracyTests
    {
        private const int IdTenant = 7;

        [Fact]
        public void ANameThatRepeatsTheWholeCar_ScoresFull()
        {
            var renegade = Car("Jeep", "Renegade", "1.8 Longitude", TransmissionType.Automatic);

            var accuracy = FipeModelMatcher.Accuracy(
                "Renegade Longitude 1.8 4x2 Flex 16V Aut.", renegade, yearConfirmed: true);

            // Os dois termos da versão, o câmbio, o combustível e o ano: tudo o que havia para
            // conferir neste carro está escrito neste nome.
            accuracy.Should().Be(100);
        }

        [Fact]
        public void TheYearMissing_CostsTheStrongestSignalThereIs()
        {
            var renegade = Car("Jeep", "Renegade", "1.8 Longitude", TransmissionType.Automatic);
            const string name = "Renegade Longitude 1.8 4x2 Flex 16V Aut.";

            var withYear = FipeModelMatcher.Accuracy(name, renegade, yearConfirmed: true);
            var without = FipeModelMatcher.Accuracy(name, renegade, yearConfirmed: false);

            // 8 de 8 contra 6 de 8. O candidato que a tabela jamais precificou no ano do carro
            // chega à tela como o mais frágil da lista — e continua alcançável, porque recusar a
            // escolha seria pior do que mostrá-la fraca.
            withYear.Should().Be(100);
            without.Should().Be(75);
        }

        [Fact]
        public void ACarWithNoVersion_NeverScoresFull_HoweverWellTheNameFits()
        {
            var gol = Car("Volkswagen", "Gol", version: null, TransmissionType.Manual);

            var accuracy = FipeModelMatcher.Accuracy(
                "Gol 1.0 Flex 8V 5p", gol, yearConfirmed: true);

            // O carro cadastrado com pressa — só "Gol" — bate tudo o que havia para bater, e
            // ainda assim metade dele segue por conferir. Marcar 100% aqui poria vinte
            // candidatos empatados no topo do medidor, que é a única leitura que esta tela
            // jamais pode dar.
            accuracy.Should().Be(50);
        }

        [Fact]
        public void HalfTheVersionFound_ScoresHalfOfWhatTheVersionIsWorth()
        {
            var onix = Car("Chevrolet", "Onix", "1.4 LTZ", TransmissionType.Manual);

            var cheio = FipeModelMatcher.Accuracy(
                "ONIX HATCH LTZ 1.4 8V FlexPower 5p Mec.", onix, yearConfirmed: true);

            var metade = FipeModelMatcher.Accuracy(
                "ONIX HATCH LT 1.4 8V FlexPower 5p Mec.", onix, yearConfirmed: true);

            // "1.4" está nos dois nomes; "LTZ" está em um só. A diferença entre os dois é
            // exatamente o que separa uma linha de preço da outra, e é ela que o medidor mostra.
            cheio.Should().Be(100);
            metade.Should().Be(75);
            metade.Should().BeLessThan(cheio);
        }

        [Fact]
        public void TheGearboxOfTheCar_SeparatesTwoNamesOfTheSameEngine()
        {
            var manual = Car("Volkswagen", "Gol", "1.6 MSI", TransmissionType.Manual);

            var semMarca = FipeModelMatcher.Accuracy(
                "Gol 1.6 MSI Flex 8V 5p", manual, yearConfirmed: true);

            var comMarca = FipeModelMatcher.Accuracy(
                "Gol 1.6 MSI Flex 16V 5p Aut.", manual, yearConfirmed: true);

            // A tabela marca o automático e deixa o manual sem marca nenhuma: um carro de câmbio
            // manual pontua pela ausência da marca, e é assim que as duas linhas do mesmo motor
            // deixam de empatar.
            semMarca.Should().Be(100);
            comMarca.Should().BeLessThan(semMarca);
        }

        [Fact]
        public void AFuelTheTableNeverWrites_IsLeftOutOfTheCount()
        {
            var gnv = Vehicle.Create(
                IdTenant, "ABC1D23", "9BWZZZ377VT004251", "Fiat", "Uno", 2020, 2019);

            gnv.SetDetails("1.0 Way", "Branco", FuelType.Gas, TransmissionType.Manual, null, null);

            var accuracy = FipeModelMatcher.Accuracy(
                "Uno Way 1.0 8V Flex 5p", gnv, yearConfirmed: true);

            // A tabela jamais escreve GNV no nome do modelo. Cobrar do candidato um ponto
            // impossível baixaria a nota de todos pelo mesmo motivo — que é o mesmo que dizer
            // nada. Fora da conta, os outros sinais respondem inteiros.
            accuracy.Should().Be(100);
        }

        [Fact]
        public void ANameOfAnotherCarEntirely_ScoresOnlyWhatItAccidentallyShares()
        {
            var renegade = Car("Jeep", "Renegade", "1.8 Longitude", TransmissionType.Automatic);

            var accuracy = FipeModelMatcher.Accuracy(
                "Renegade 2.0 4x4 TB Diesel Aut.", renegade, yearConfirmed: true);

            // Nenhum termo da versão, e o combustível errado: sobram o câmbio e o ano. Uma nota
            // baixa que continua na lista é a resposta certa — quem conhece o carro descarta
            // isto numa olhada, e o sistema jamais descartaria por conta própria.
            accuracy.Should().Be(38);
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
