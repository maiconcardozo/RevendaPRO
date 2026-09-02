using FluentAssertions;
using RevendaPro.Domain.Entities;
using RevendaPro.Domain.Enums;
using RevendaPro.Domain.ValueObjects;
using RevendaPro.Shared.Exceptions;
using RevendaPro.Shared.Helpers;

namespace RevendaPro.Tests.Unit
{
    /// <summary>The rules a vehicle enforces on its own.</summary>
    public class VehicleRulesTests
    {
        [Theory]
        [InlineData("ABC1234")]      // formato antigo
        [InlineData("ABC1D23")]      // Mercosul
        [InlineData("abc-1234")]     // minúscula e hífen
        public void ValidPlates_AreAccepted(string plate) =>
            VehicleIdentifiers.IsValidPlate(plate).Should().BeTrue();

        [Theory]
        [InlineData("ABCD123")]      // quatro letras
        [InlineData("AB12345")]      // duas letras
        [InlineData("ABC12D3")]      // letra na posição errada
        [InlineData("ABC123")]       // curta
        [InlineData("")]
        public void InvalidPlates_AreRefused(string plate) =>
            VehicleIdentifiers.IsValidPlate(plate).Should().BeFalse();

        [Fact]
        public void ChassisRefusesTheThreeLettersTheStandardExcludes()
        {
            // I, O e Q ficam de fora justamente para ninguém ler 1 e 0 no lugar delas.
            VehicleIdentifiers.IsValidChassis("9BWZZZ377VT004251").Should().BeTrue();

            VehicleIdentifiers.IsValidChassis("9BWZZZ377VT00425I").Should().BeFalse();
            VehicleIdentifiers.IsValidChassis("9BWZZZ377VT00425O").Should().BeFalse();
            VehicleIdentifiers.IsValidChassis("9BWZZZ377VT00425Q").Should().BeFalse();

            // Dezesseis caracteres.
            VehicleIdentifiers.IsValidChassis("9BWZZZ377VT00425").Should().BeFalse();
        }

        [Fact]
        public void ModelYearBeforeManufactureYear_IsRefused()
        {
            // Um carro é fabricado num ano e vendido como modelo do ano seguinte. O contrário
            // não existe.
            var act = () => Vehicle.Create(1, "ABC1D23", Chassis, "Chevrolet", "Cruze", 2013, 2014);

            act.Should().Throw<BusinessRuleException>();
        }

        [Fact]
        public void PlateAndChassis_AreStoredBare()
        {
            var vehicle = Vehicle.Create(
                1, "abc-1d23", "9bwzzz377vt004251", "Chevrolet", "Cruze", 2014, 2014);

            vehicle.Plate.Should().Be("ABC1D23");
            vehicle.Chassis.Should().Be("9BWZZZ377VT004251");
        }

        [Fact]
        public void DamageWithoutADescription_IsRefused()
        {
            // A descrição é o que acompanha as fotos do dano quando elas vão para o comprador.
            var vehicle = SampleVehicle();

            var act = () => vehicle.SetOrigin(VehicleOrigin.Auction, hasDamage: true, null);

            act.Should().Throw<BusinessRuleException>();
        }

        [Fact]
        public void Mileage_OnlyGoesUp()
        {
            var vehicle = SampleVehicle();
            vehicle.UpdateMileage(90_000);

            var act = () => vehicle.UpdateMileage(80_000);

            act.Should().Throw<BusinessRuleException>()
                .WithMessage("*90000 km*", "a mensagem diz qual é a quilometragem atual");

            // Correção continua possível, e é deliberada.
            vehicle.UpdateMileage(80_000, correction: true);
            vehicle.Mileage.Should().Be(80_000);
        }

        [Fact]
        public void TheStatusPipeline_FollowsTheBusiness()
        {
            var vehicle = SampleVehicle();

            vehicle.Status.Should().Be(VehicleStatus.UnderReview);

            vehicle.ChangeStatus(VehicleStatus.Purchased);
            vehicle.ChangeStatus(VehicleStatus.InRepair);
            vehicle.ChangeStatus(VehicleStatus.ReadyForSale);
            vehicle.ChangeStatus(VehicleStatus.Advertised);
            vehicle.ChangeStatus(VehicleStatus.Negotiating);

            // "Vendido" só se alcança pela venda. Ver SaleRulesTests.
            vehicle.Sell();

            vehicle.Status.Should().Be(VehicleStatus.Sold);
        }

        [Fact]
        public void GoingBackIsAllowedWhereTheBusinessGoesBack()
        {
            var vehicle = SampleVehicle();
            vehicle.ChangeStatus(VehicleStatus.Purchased);
            vehicle.ChangeStatus(VehicleStatus.ReadyForSale);
            vehicle.ChangeStatus(VehicleStatus.Advertised);

            // Apareceu algo depois de pronto: volta para a oficina.
            vehicle.ChangeStatus(VehicleStatus.InRepair);
            vehicle.Status.Should().Be(VehicleStatus.InRepair);

            // Negociação que desanda devolve o carro ao mercado.
            vehicle.ChangeStatus(VehicleStatus.ReadyForSale);
            vehicle.ChangeStatus(VehicleStatus.Negotiating);
            vehicle.ChangeStatus(VehicleStatus.Advertised);
            vehicle.Status.Should().Be(VehicleStatus.Advertised);
        }

        [Fact]
        public void SkippingTheLineIsRefused_AndTheMessageSaysWhereItCanGo()
        {
            var vehicle = SampleVehicle();

            var act = () => vehicle.ChangeStatus(VehicleStatus.Sold);

            act.Should().Throw<BusinessRuleException>()
                .WithMessage("*Em análise*")
                .WithMessage("*Comprado*");
        }

        [Fact]
        public void ASoldVehicle_StaysSold()
        {
            var vehicle = SampleVehicle();
            vehicle.ChangeStatus(VehicleStatus.Purchased);
            vehicle.ChangeStatus(VehicleStatus.ReadyForSale);
            vehicle.ChangeStatus(VehicleStatus.Negotiating);
            vehicle.Sell();

            var act = () => vehicle.ChangeStatus(VehicleStatus.Advertised);

            act.Should().Throw<BusinessRuleException>();
        }

        [Fact]
        public void ChangeStatus_AnswersWhereItCameFrom_SoTheHistoryIsComplete()
        {
            var vehicle = SampleVehicle();

            var previous = vehicle.ChangeStatus(VehicleStatus.Purchased);

            previous.Should().Be(VehicleStatus.UnderReview);
        }

        [Fact]
        public void MinimumPriceAboveTheDesiredPrice_IsRefused()
        {
            var vehicle = SampleVehicle();

            var act = () => vehicle.SetPricing(58_000, 60_000, null, null);

            act.Should().Throw<BusinessRuleException>();
        }

        [Fact]
        public void FipeValueWithoutAReferenceMonth_IsRefused()
        {
            // A tabela muda todo mês, então o valor sozinho não diz nada.
            var vehicle = SampleVehicle();

            var act = () => vehicle.SetFipe(66_000, null, null);

            act.Should().Throw<BusinessRuleException>();
        }

        [Fact]
        public void DaysInStock_CountFromThePurchase()
        {
            var vehicle = SampleVehicle();

            vehicle.DaysInStock(new DateOnly(2026, 9, 1)).Should().BeNull();

            vehicle.SetPurchase(29_450, new DateOnly(2026, 7, 3), "Leilão", PaymentMethod.BankTransfer);

            vehicle.DaysInStock(new DateOnly(2026, 9, 1)).Should().Be(60);
        }

        private static Vehicle SampleVehicle() =>
            Vehicle.Create(1, "ABC1D23", Chassis, "Chevrolet", "Cruze", 2014, 2014);

        private const string Chassis = "9BWZZZ377VT004251";
    }

    /// <summary>
    /// O cálculo de custo, conferido contra a planilha real do stakeholder.
    /// </summary>
    public class VehicleCostTests
    {
        /// <summary>
        /// As despesas do Cruze, exatamente como estão no GASTOS.docx dele, e na mesma ordem.
        /// A linha do carro fica de fora: ela é a compra.
        /// </summary>
        private static readonly (string Description, decimal Amount)[] RealSpendingSheet =
        [
            ("FRETE", 1_700), ("Paralama", 300), ("Amortecedor bandeja sensor", 500),
            ("Caixa ressonador", 170), ("Pisca", 36), ("Kit correia dentada", 447),
            ("Amortecedor", 433), ("Moldura milha", 250), ("Mecânica", 1_600),
            ("Farol de milha", 77), ("Parachoque", 480), ("Pneu", 490), ("Lampada", 95),
            ("Guia parachoque", 45), ("Lata e pintura", 800), ("Polimento", 700),
            ("DOC FRETE", 50), ("Filtro", 21),
            // As três que entraram DEPOIS do total escrito à mão.
            ("Banco", 150), ("LAMPADAS", 140), ("Alinhamento", 60)
        ];

        [Fact]
        public void TheRealSpendingSheet_TotalsWhatTheDocumentFailedToShow()
        {
            var vehicle = Vehicle.Create(1, "ABC1D23", "9BWZZZ377VT004251", "Chevrolet", "Cruze", 2014, 2014);
            vehicle.SetPurchase(29_450, new DateOnly(2026, 7, 3), "Leilão", PaymentMethod.BankTransfer);
            vehicle.SetBudgetCeiling(40_000);

            var expenses = RealSpendingSheet
                .Select(line => VehicleExpense.Create(
                    1, line.Description, PartsTypeId, line.Amount, new DateOnly(2026, 7, 10)))
                .ToList();

            var cost = VehicleCost.Of(vehicle, expenses);

            // O documento dele mostra 37.644, porque o total foi escrito antes das três
            // últimas linhas. O custo real é 350 maior.
            cost.Total.Should().Be(37_994m);
            (cost.Total - 37_644m).Should().Be(350m);

            // O teto era 40 mil.
            cost.BudgetRemaining.Should().Be(2_006m);
            cost.BudgetUsedPercent.Should().Be(94.99m);
            cost.IsOverBudget.Should().BeFalse();
        }

        [Fact]
        public void PlannedExpenses_StayOutOfTheRealCost_AndShowUpInTheProjection()
        {
            var vehicle = VehicleWithPurchase(29_450);

            var cost = VehicleCost.Of(vehicle, [
                Paid(1_700),
                Planned(2_000)
            ]);

            cost.Total.Should().Be(31_150m);
            cost.Projected.Should().Be(33_150m);
        }

        [Fact]
        public void TheWarningArrivesWhileThereIsStillAChoice()
        {
            // Ainda cabe no teto hoje, e o que está previsto estoura. É esse o aviso útil.
            var vehicle = VehicleWithPurchase(29_450);
            vehicle.SetBudgetCeiling(35_000);

            var cost = VehicleCost.Of(vehicle, [Paid(3_000), Planned(4_000)]);

            cost.IsOverBudget.Should().BeFalse();
            cost.WillExceedBudget.Should().BeTrue();
            cost.BudgetRemaining.Should().Be(2_550m);
        }

        [Fact]
        public void OverTheCeiling_TheRemainingRoomGoesNegative()
        {
            var vehicle = VehicleWithPurchase(29_450);
            vehicle.SetBudgetCeiling(30_000);

            var cost = VehicleCost.Of(vehicle, [Paid(2_000)]);

            cost.IsOverBudget.Should().BeTrue();
            cost.BudgetRemaining.Should().Be(-1_450m);
            cost.BudgetUsedPercent.Should().Be(104.83m);
        }

        [Fact]
        public void TheDecisionFromTheInterview()
        {
            // "Quero 58, o carro me custa 40, o cara manda 55 no dinheiro.
            //  Ganhar 15 mil? Já dou-lhe fogo."
            var vehicle = VehicleWithPurchase(40_000);

            var cost = VehicleCost.Of(vehicle, []);

            cost.ProfitAt(55_000).Should().Be(15_000m);
            cost.MarginAt(55_000).Should().Be(27.27m);
        }

        [Fact]
        public void PercentOfFipe_IsInformationAndNeverTheDecision()
        {
            var vehicle = VehicleWithPurchase(40_000);
            vehicle.SetFipe(66_000, new DateOnly(2026, 9, 1), null);

            var cost = VehicleCost.Of(vehicle, []);

            cost.PercentOfFipe.Should().Be(60.61m);
        }

        [Fact]
        public void WithoutACeiling_TheBudgetNumbersStayEmpty()
        {
            var cost = VehicleCost.Of(VehicleWithPurchase(29_450), []);

            cost.BudgetRemaining.Should().BeNull();
            cost.BudgetUsedPercent.Should().BeNull();
            cost.IsOverBudget.Should().BeFalse();
            cost.WillExceedBudget.Should().BeFalse();
        }

        private static Vehicle VehicleWithPurchase(decimal price)
        {
            var vehicle = Vehicle.Create(
                1, "ABC1D23", "9BWZZZ377VT004251", "Chevrolet", "Cruze", 2014, 2014);

            vehicle.SetPurchase(price, new DateOnly(2026, 7, 3), "Leilão", PaymentMethod.Cash);

            return vehicle;
        }

        /// <summary>Id do tipo "Peças" nos testes. O tipo agora e tabela, e nao enum.</summary>
        private const int PartsTypeId = 1;

        private static VehicleExpense Paid(decimal amount) =>
            VehicleExpense.Create(1, "Peça", PartsTypeId, amount, new DateOnly(2026, 7, 10));

        private static VehicleExpense Planned(decimal amount) =>
            VehicleExpense.Create(
                1, "Prevista", PartsTypeId, amount, new DateOnly(2026, 7, 20), isPaid: false);
    }
}
