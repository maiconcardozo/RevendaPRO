using FluentAssertions;
using RevendaPro.Domain.Entities;
using RevendaPro.Domain.Enums;
using RevendaPro.Infrastructure.Database;
using RevendaPro.Shared.Helpers;

namespace RevendaPro.Tests.Unit
{
    /// <summary>
    /// O pátio de demonstração, conferido antes de o contêiner subir.
    ///
    /// <i>"Crie uns carros com nomes meio genéricos para mais testes, pode criar mais uns 20 em
    /// pátios variados com variações de lucro e prejuízo."</i>
    ///
    /// <b>É um catálogo, e catálogo é dado.</b> Uma placa com um caractere a mais, um pátio
    /// escrito de outro jeito ou um carro vendido que a esteira jamais alcança falhariam lá na
    /// primeira subida de uma máquina nova — num log que ninguém lê, e com o pátio pela metade.
    /// Aqui o mesmo erro custa um teste vermelho em milissegundos.
    ///
    /// Nada aqui toca banco nem rede: o catálogo passa pelas <b>mesmas regras do domínio</b> que
    /// o semeador vai usar.
    /// </summary>
    public class DemoYardTests
    {
        private const int IdTenant = 7;

        [Fact]
        public void TheYard_HasTwentyCarsInFourPlaces()
        {
            DemoYard.Cars.Should().HaveCount(20);
            DemoYard.Places.Should().HaveCount(4);

            // Um pátio da casa e três de terceiros: é o que o M14 separou, e é o que faz o
            // relatório por lugar ter o que mostrar.
            DemoYard.Places.Should().ContainSingle(place => place.Kind == YardKind.Own);
            DemoYard.Places.Count(place => place.Kind == YardKind.Partner).Should().Be(3);
        }

        [Fact]
        public void EveryCar_PassesTheRulesOfTheDomain()
        {
            foreach (var car in DemoYard.Cars)
            {
                VehicleIdentifiers.IsValidPlate(car.Plate).Should().BeTrue($"a placa {car.Plate}");
                VehicleIdentifiers.IsValidChassis(car.Chassis).Should().BeTrue($"o chassi de {car.Plate}");

                // Construir o carro é a prova real: quilometragem, compra e identificação
                // passam pelas mesmas exceções que qualquer cadastro feito na tela.
                var vehicle = Vehicle.Create(
                    IdTenant, car.Plate, car.Chassis, car.Brand, car.Model,
                    car.ModelYear, (short)(car.ModelYear - 1));

                vehicle.SetDetails(car.Version, car.Color, car.Fuel, car.Transmission, null, null);
                vehicle.UpdateMileage(car.Mileage);
                vehicle.SetPurchase(car.PurchasePrice, new DateOnly(2026, 1, 10), car.Supplier, PaymentMethod.BankTransfer);

                vehicle.PurchasePrice.Should().BeGreaterThan(0);
            }
        }

        [Fact]
        public void PlatesAndChassis_AreUniqueAcrossTheYard()
        {
            // A empresa recusa placa e chassi repetidos, e o semeador pula o que já existe.
            // Duas linhas iguais aqui virariam dezenove carros e um silêncio.
            DemoYard.Cars.Select(car => car.Plate).Distinct().Should().HaveCount(20);
            DemoYard.Cars.Select(car => car.Chassis).Distinct().Should().HaveCount(20);
        }

        [Fact]
        public void EveryCar_SitsInAPlaceThatExists()
        {
            var places = DemoYard.Places.Select(place => place.Name).ToHashSet();

            DemoYard.Cars.Should().OnlyContain(car => places.Contains(car.Place));

            // E os quatro lugares têm carro: um pátio vazio na demonstração é uma tela vazia
            // para quem abrir o relatório por lugar.
            foreach (var place in places)
            {
                DemoYard.Cars.Should().Contain(car => car.Place == place, $"o pátio {place}");
            }
        }

        [Fact]
        public void TheYard_ShowsBothEndsOfTheFipeSearch()
        {
            var comVersao = DemoYard.Cars.Count(car => car.Version is not null);
            var semVersao = DemoYard.Cars.Count(car => car.Version is null);

            // O carro com versão escrita é o que a busca resolve em um ou poucos candidatos. O
            // cadastrado só com o nome — "Gol", "Uno" — é o que devolve dez, quinze, vinte, e é
            // onde o medidor de acurácia mostra a lista inteira empatada em 50%.
            comVersao.Should().BeGreaterThanOrEqualTo(8);
            semVersao.Should().BeGreaterThanOrEqualTo(8);
        }

        [Fact]
        public void TheYard_ShowsProfitAndAlsoLoss()
        {
            var sold = DemoYard.Cars.Where(car => car.Sale is not null).ToList();

            sold.Should().HaveCountGreaterThanOrEqualTo(5);

            var margens = sold
                .Select(car => car.Sale!.Amount
                    - car.PurchasePrice
                    - car.Expenses.Sum(expense => expense.Amount)
                    - car.Sale!.Commission)
                .ToList();

            // Um pátio só com lucro conta uma história falsa. O prejuízo é o que faz o painel de
            // custo valer alguma coisa — e nestes dois carros ele tem nome: o reparo comeu a
            // margem, e o custo somado explica o resto.
            margens.Should().Contain(margem => margem > 0);
            margens.Should().Contain(margem => margem < 0);
        }

        [Fact]
        public void EverySoldCar_ReachesTheEndOfThePipeline()
        {
            foreach (var car in DemoYard.Cars.Where(car => car.Sale is not null))
            {
                car.Status.Should().Be(VehicleStatus.Sold, $"o carro {car.Plate}");
            }

            // E o contrário também: um carro parado no pátio jamais carrega venda.
            DemoYard.Cars
                .Where(car => car.Status != VehicleStatus.Sold)
                .Should().OnlyContain(car => car.Sale == null);
        }

        [Fact]
        public void TheStatuses_SpreadAcrossThePipeline()
        {
            var estagios = DemoYard.Cars.Select(car => car.Status).Distinct().ToList();

            // A esteira só se entende com carro em cada degrau: comprado, em reparo, pronto,
            // anunciado, negociando e vendido.
            estagios.Should().HaveCountGreaterThanOrEqualTo(5);
            estagios.Should().Contain(VehicleStatus.Sold);
            estagios.Should().Contain(VehicleStatus.InRepair);
        }
    }
}
