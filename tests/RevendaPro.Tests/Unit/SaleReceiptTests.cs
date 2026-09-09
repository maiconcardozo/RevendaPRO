using FluentAssertions;
using RevendaPro.Domain.Entities;
using RevendaPro.Domain.Enums;

namespace RevendaPro.Tests.Unit
{
    /// <summary>
    /// O que entra por uma venda (M22).
    ///
    /// O esperado em dinheiro é o valor menos o que jamais vira depósito: o carro que entrou na
    /// troca, que já está no pátio, e o repasse da loja parceira, que é dela. O que se prova
    /// aqui é essa conta, e a entrada que a própria forma de pagamento descreve.
    /// </summary>
    public class SaleReceiptTests
    {
        private static readonly DateOnly Dia = new(2026, 9, 1);

        private static Sale SaleOf(
            PaymentMethod method,
            decimal amount = 50_000m,
            decimal? tradeInValue = null,
            decimal? partnerCutPercent = null) =>
            Sale.Create(
                idVehicle: 1, idProposal: null, Dia, amount, method,
                partnerCutPercent is null ? SaleChannel.Direct : SaleChannel.PartnerStore,
                partnerCutPercent is null ? null : "Loja do Joãozinho",
                partnerCutPercent, partnerCutAmount: null, commission: 0m, commissionNotes: null,
                "Marcos", null, null, tradeInValue, null);

        [Fact]
        public void OEsperadoEmDinheiro_TiraOCarroDaTroca_EORepasseDaLoja()
        {
            SaleOf(PaymentMethod.Cash).ExpectedCash.Should().Be(50_000m);

            SaleOf(PaymentMethod.TradeInWithCash, tradeInValue: 20_000m)
                .ExpectedCash.Should().Be(30_000m, "o carro que entrou já está no pátio");

            SaleOf(PaymentMethod.TradeIn, amount: 20_000m, tradeInValue: 20_000m)
                .ExpectedCash.Should().Be(0m, "troca pura jamais vira depósito");

            SaleOf(PaymentMethod.Cash, partnerCutPercent: 10m)
                .ExpectedCash.Should().Be(45_000m, "o repasse é da loja parceira, e nunca passa pela conta");
        }

        [Fact]
        public void AVendaAVista_NasceComAEntradaDoDia()
        {
            var sale = SaleOf(PaymentMethod.BankTransfer);

            var receipt = sale.FirstReceipt("eu");

            receipt.Should().NotBeNull();
            receipt!.Amount.Should().Be(50_000m);
            receipt.Date.Should().Be(Dia);
            receipt.PaymentMethod.Should().Be(PaymentMethod.BankTransfer);
            sale.DueDate.Should().BeNull("dinheiro nenhum ficou para depois");
        }

        [Fact]
        public void AVendaFinanciada_NasceSemEntrada_EComPrazoDeUmaSemana()
        {
            var sale = SaleOf(PaymentMethod.Financing);

            var receipt = sale.FirstReceipt("eu");

            receipt.Should().BeNull("o banco paga depois");
            sale.DueDate.Should().Be(Dia.AddDays(7));
        }

        [Fact]
        public void ATrocaPura_NasceSemEntrada_ESemPrazo()
        {
            var sale = SaleOf(PaymentMethod.TradeIn, amount: 20_000m, tradeInValue: 20_000m);

            sale.FirstReceipt("eu")
                .Should().BeNull();

            sale.DueDate.Should().BeNull();
        }
    }
}
