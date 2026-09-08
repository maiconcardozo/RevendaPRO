using RevendaPro.Domain.Enums;

namespace RevendaPro.Api.Reports
{
    /// <summary>
    /// O nome de cada valor de enum como o papel o escreve. Os mesmos rótulos da tela, que
    /// moram em <c>frontend/lib/types.ts</c>: a API fala números, e quem lê o documento é gente.
    /// </summary>
    public static class Labels
    {
        /// <summary>Flex, Gasolina, Etanol, Diesel, Híbrido, Elétrico, GNV.</summary>
        public static string Of(FuelType fuel) => fuel switch
        {
            FuelType.Flex => "Flex",
            FuelType.Gasoline => "Gasolina",
            FuelType.Ethanol => "Etanol",
            FuelType.Diesel => "Diesel",
            FuelType.Hybrid => "Híbrido",
            FuelType.Electric => "Elétrico",
            FuelType.Gas => "GNV",
            _ => fuel.ToString(),
        };

        /// <summary>Manual, Automático, Automatizado, CVT.</summary>
        public static string Of(TransmissionType transmission) => transmission switch
        {
            TransmissionType.Manual => "Manual",
            TransmissionType.Automatic => "Automático",
            TransmissionType.AutomatedManual => "Automatizado",
            TransmissionType.Cvt => "CVT",
            _ => transmission.ToString(),
        };

        /// <summary>Como o cliente paga.</summary>
        public static string Of(PaymentMethod method) => method switch
        {
            PaymentMethod.Cash => "À vista",
            PaymentMethod.BankTransfer => "Transferência ou Pix",
            PaymentMethod.Financing => "Financiamento",
            PaymentMethod.Card => "Cartão",
            PaymentMethod.TradeIn => "Troca por veículo",
            PaymentMethod.TradeInWithCash => "Troca por veículo mais dinheiro",
            PaymentMethod.Other => "A combinar",
            _ => method.ToString(),
        };

        /// <summary>Onde o carro está na esteira.</summary>
        public static string Of(VehicleStatus status) => status switch
        {
            VehicleStatus.UnderReview => "Em análise",
            VehicleStatus.Purchased => "Comprado",
            VehicleStatus.InRepair => "Em reparo",
            VehicleStatus.ReadyForSale => "Pronto para venda",
            VehicleStatus.Advertised => "Anunciado",
            VehicleStatus.Negotiating => "Em negociação",
            VehicleStatus.Sold => "Vendido",
            _ => status.ToString(),
        };

        /// <summary>De onde o carro veio.</summary>
        public static string Of(VehicleOrigin origin) => origin switch
        {
            VehicleOrigin.Auction => "Leilão",
            VehicleOrigin.Individual => "Particular",
            VehicleOrigin.Store => "Loja",
            VehicleOrigin.TradeIn => "Troca",
            VehicleOrigin.Other => "Outra",
            _ => origin.ToString(),
        };

        /// <summary>Por onde a venda saiu.</summary>
        public static string Of(SaleChannel channel) => channel switch
        {
            SaleChannel.Direct => "Direta",
            SaleChannel.PartnerStore => "Loja parceira",
            _ => channel.ToString(),
        };
    }
}
