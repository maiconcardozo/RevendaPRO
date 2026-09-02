using FluentAssertions;
using RevendaPro.Domain.Entities;
using RevendaPro.Domain.Enums;
using RevendaPro.Domain.ValueObjects;
using RevendaPro.Shared.Exceptions;

namespace RevendaPro.Tests.Unit
{
    /// <summary>
    /// Proposta, venda e o lucro nas duas pontas.
    ///
    /// O decisor do stakeholder, em números: o Cruze custou 37.994, ele quer 58, e chega
    /// uma proposta de 55 no dinheiro. Tudo aqui gira em torno de responder "quanto sobra" com
    /// a mesma conta antes e depois da venda.
    /// </summary>
    public class SaleRulesTests
    {
        private const string Chassis = "9BWZZZ377VT004251";
        private static readonly DateOnly Today = new(2026, 9, 2);

        [Fact]
        public void TheProposalOfFiftyFive_LeavesSeventeenThousand()
        {
            // "O carro me custa 40 e o cara me manda 55 no dinheiro. Ganhar 15 mil? Já dou-lhe
            // fogo." O custo real é 37.994, então sobra ainda mais do que ele estimou de cabeça.
            var cost = CruzeCost();

            var proposal = Proposal.Create(
                1, "Cara do Marketplace", "47999990000", 55_000m, Today,
                PaymentMethod.Cash, SaleChannel.Direct, null, null, null);

            var result = proposal.ResultAgainst(cost);

            result.Received.Should().Be(55_000m);
            result.NetProfit.Should().Be(17_006m);
            result.Margin.Should().Be(30.92m);
        }

        [Fact]
        public void ThroughAPartnerStore_TheCutComesOffWhatHeReceives()
        {
            // "Eu quero 58 para mim. A loja põe dela em cima." A loja anuncia por 63, fica com
            // 5, e os 58 chegam nele. O lucro é sobre os 58, e jamais sobre os 63.
            var cost = CruzeCost();

            var proposal = Proposal.Create(
                1, "Cliente da loja", null, 63_000m, Today,
                PaymentMethod.Financing, SaleChannel.PartnerStore, null, 5_000m, null);

            var result = proposal.ResultAgainst(cost);

            result.Received.Should().Be(58_000m);
            result.NetProfit.Should().Be(20_006m);
        }

        [Fact]
        public void ThePartnerCut_CanBeAPercentage()
        {
            var proposal = Proposal.Create(
                1, "Cliente da loja", null, 60_000m, Today,
                PaymentMethod.Cash, SaleChannel.PartnerStore, 8m, null, null);

            proposal.PartnerCut.Should().Be(4_800m);
        }

        [Fact]
        public void PercentageAndAmountTogether_AreRefused()
        {
            // A loja acertou de um jeito ou de outro. Os dois ao mesmo tempo é erro de digitação.
            var act = () => Proposal.Create(
                1, "Cliente", null, 60_000m, Today,
                PaymentMethod.Cash, SaleChannel.PartnerStore, 8m, 5_000m, null);

            act.Should().Throw<BusinessRuleException>();
        }

        [Fact]
        public void ADirectProposal_IgnoresAnyCut()
        {
            var proposal = Proposal.Create(
                1, "Vizinho", null, 55_000m, Today,
                PaymentMethod.Cash, SaleChannel.Direct, 8m, null, null);

            proposal.PartnerCut.Should().Be(0);
            proposal.PartnerCutPercent.Should().BeNull();
        }

        [Fact]
        public void OnlyAnOpenProposal_CanBeAccepted()
        {
            var proposal = Proposal.Create(
                1, "Cliente", null, 55_000m, Today,
                PaymentMethod.Cash, SaleChannel.Direct, null, null, null);

            proposal.Decline();

            var act = () => proposal.Accept();

            act.Should().Throw<BusinessRuleException>();
        }

        [Fact]
        public void AnAcceptedProposal_RefusesToBeDeclined_UntilTheSaleIsUndone()
        {
            var proposal = Proposal.Create(
                1, "Cliente", null, 55_000m, Today,
                PaymentMethod.Cash, SaleChannel.Direct, null, null, null);

            proposal.Accept();

            var act = () => proposal.Decline();

            act.Should().Throw<BusinessRuleException>().WithMessage("*Cancele a venda*");

            proposal.Reopen();
            proposal.Status.Should().Be(ProposalStatus.Open);
        }

        [Fact]
        public void TheSale_AnswersTheSameNumberTheProposalPromised()
        {
            // A promessa da proposta e o relatório da venda saem da mesma conta. Se um dia
            // discordarem, a pessoa deixa de confiar nos dois.
            var cost = CruzeCost();

            var proposal = Proposal.Create(
                1, "Cliente", null, 55_000m, Today,
                PaymentMethod.Cash, SaleChannel.Direct, null, null, null);

            var sale = Sale.Create(
                1, null, Today, 55_000m, PaymentMethod.Cash, SaleChannel.Direct,
                null, null, null, commission: 0, null, "Cliente", "52998224725", null, null, null);

            sale.ResultAgainst(cost).NetProfit.Should().Be(proposal.ResultAgainst(cost).NetProfit);
        }

        [Fact]
        public void CommissionComesOffTheNetProfit_AndNeverOffTheGross()
        {
            var cost = CruzeCost();

            var sale = Sale.Create(
                1, null, Today, 60_000m, PaymentMethod.BankTransfer, SaleChannel.PartnerStore,
                "Loja do Thiago", null, 4_000m, commission: 1_000m, "Indicação do Clei",
                "Comprador", null, null, null, null);

            var result = sale.ResultAgainst(cost);

            result.GrossProfit.Should().Be(22_006m);
            result.Received.Should().Be(56_000m);
            result.NetProfit.Should().Be(17_006m);
        }

        [Fact]
        public void APartnerSale_AlwaysKeepsTheCutInMoney()
        {
            // O percentual é como foi acertado; o valor é o que saiu da conta. O que fica
            // guardado é o que saiu.
            var sale = Sale.Create(
                1, null, Today, 60_000m, PaymentMethod.Cash, SaleChannel.PartnerStore,
                "Loja", 10m, null, 0, null, "Comprador", null, null, null, null);

            sale.PartnerCutPercent.Should().Be(10m);
            sale.PartnerCutAmount.Should().Be(6_000m);
        }

        [Fact]
        public void APartnerSale_NeedsTheStoreName()
        {
            var act = () => Sale.Create(
                1, null, Today, 60_000m, PaymentMethod.Cash, SaleChannel.PartnerStore,
                "  ", 10m, null, 0, null, "Comprador", null, null, null, null);

            act.Should().Throw<BusinessRuleException>().WithMessage("*loja parceira*");
        }

        [Fact]
        public void AnInvalidBuyerDocument_IsRefused()
        {
            var act = () => Sale.Create(
                1, null, Today, 55_000m, PaymentMethod.Cash, SaleChannel.Direct,
                null, null, null, 0, null, "Comprador", "11111111111", null, null, null);

            act.Should().Throw<BusinessRuleException>().WithMessage("*CPF ou CNPJ*");
        }

        [Fact]
        public void ATrade_SplitsThePriceBetweenCarAndCash_AndTheProfitDoesNotMove()
        {
            // "Pode ser troca que gera uma entrada, ou um carro e um dinheiro." 55 fechados:
            // 20 em carro, 35 em dinheiro. O lucro é o mesmo de 55 no dinheiro — mudou a forma.
            var cost = CruzeCost();

            var sale = Sale.Create(
                1, null, Today, 55_000m, PaymentMethod.TradeInWithCash, SaleChannel.Direct,
                null, null, null, 0, null, "Comprador", null, null, tradeInValue: 20_000m, null);

            sale.CashAmount.Should().Be(35_000m);
            sale.ResultAgainst(cost).NetProfit.Should().Be(17_006m);
        }

        [Fact]
        public void ATradeWithoutCash_IsWorthExactlyThePrice()
        {
            var act = () => Sale.Create(
                1, null, Today, 55_000m, PaymentMethod.TradeIn, SaleChannel.Direct,
                null, null, null, 0, null, "Comprador", null, null, tradeInValue: 20_000m, null);

            act.Should().Throw<BusinessRuleException>().WithMessage("*Troca com volta*");
        }

        [Fact]
        public void ATradeValue_DemandsATradePaymentMethod()
        {
            var act = () => Sale.Create(
                1, null, Today, 55_000m, PaymentMethod.Cash, SaleChannel.Direct,
                null, null, null, 0, null, "Comprador", null, null, tradeInValue: 20_000m, null);

            act.Should().Throw<BusinessRuleException>();
        }

        [Fact]
        public void TheIncomingCar_IsBornWithTheTradeValueAsItsPurchase()
        {
            var incoming = Vehicle.CreateFromTradeIn(
                1, "XYZ9A88", Chassis, "Fiat", "Argo", 2020, 2019,
                tradeInValue: 20_000m, Today, fromWhom: "Comprador do Cruze");

            incoming.Origin.Should().Be(VehicleOrigin.TradeIn);
            incoming.PurchasePrice.Should().Be(20_000m);
            incoming.PurchaseDate.Should().Be(Today);
            incoming.SupplierName.Should().Be("Comprador do Cruze");
            incoming.PurchasePaymentMethod.Should().Be(PaymentMethod.TradeIn);
            incoming.Status.Should().Be(VehicleStatus.UnderReview);
        }

        [Fact]
        public void SoldIsReachedThroughTheSale_AndThroughNothingElse()
        {
            var vehicle = ReadyCruze();

            // O caminho antigo, pela mudança de status, fechou.
            vehicle.CanChangeTo(VehicleStatus.Sold).Should().BeFalse();

            var act = () => vehicle.ChangeStatus(VehicleStatus.Sold);
            act.Should().Throw<BusinessRuleException>();

            vehicle.Sell().Should().Be(VehicleStatus.ReadyForSale);
            vehicle.Status.Should().Be(VehicleStatus.Sold);
        }

        [Theory]
        [InlineData(VehicleStatus.ReadyForSale)]
        [InlineData(VehicleStatus.Advertised)]
        [InlineData(VehicleStatus.Negotiating)]
        public void ACarOnTheLot_CanBeSoldFromAnyOfItsThreeStates(VehicleStatus from)
        {
            var vehicle = ReadyCruze();

            if (from != VehicleStatus.ReadyForSale)
            {
                vehicle.ChangeStatus(from);
            }

            vehicle.CanBeSold.Should().BeTrue();
            vehicle.Sell().Should().Be(from);
        }

        [Fact]
        public void ACarStillInTheWorkshop_IsRefusedToTheBuyer()
        {
            var vehicle = Vehicle.Create(1, "ABC1D23", Chassis, "Chevrolet", "Cruze", 2014, 2013);
            vehicle.ChangeStatus(VehicleStatus.Purchased);
            vehicle.ChangeStatus(VehicleStatus.InRepair);

            vehicle.CanBeSold.Should().BeFalse();

            var act = () => vehicle.Sell();

            act.Should().Throw<BusinessRuleException>().WithMessage("*Em reparo*");
        }

        [Fact]
        public void UndoingTheSale_PutsTheCarBackOnTheLot()
        {
            var vehicle = ReadyCruze();
            vehicle.Sell();

            vehicle.CancelSale();

            vehicle.Status.Should().Be(VehicleStatus.ReadyForSale);
            vehicle.CanBeSold.Should().BeTrue();
        }

        [Fact]
        public void UndoingASaleThatNeverHappened_IsRefused()
        {
            var act = () => ReadyCruze().CancelSale();

            act.Should().Throw<BusinessRuleException>();
        }

        /// <summary>O Cruze da planilha real, com o custo que o M6 provou: 37.994.</summary>
        private static VehicleCost CruzeCost() =>
            new(Purchase: 29_450m, PaidExpenses: 8_544m, PlannedExpenses: 0,
                BudgetCeiling: 40_000m, FipeValue: 66_000m);

        private static Vehicle ReadyCruze()
        {
            var vehicle = Vehicle.Create(1, "ABC1D23", Chassis, "Chevrolet", "Cruze", 2014, 2013);
            vehicle.ChangeStatus(VehicleStatus.Purchased);
            vehicle.ChangeStatus(VehicleStatus.ReadyForSale);

            return vehicle;
        }
    }
}
