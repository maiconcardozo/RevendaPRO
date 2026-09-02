using System.Diagnostics;
using RevendaPro.Domain.Enums;
using RevendaPro.Domain.ValueObjects;
using RevendaPro.Shared.Exceptions;
using RevendaPro.Shared.Helpers;

namespace RevendaPro.Domain.Entities
{
    /// <summary>
    /// The sale of a vehicle (RF-20). One per vehicle among the active rows; undoing a sale is
    /// deleting this record, and never editing the status by hand.
    ///
    /// The buyer lives here, and not in a table of its own. There is no CRM in this phase, and
    /// a table with one row per sale is ceremony. Document and phone are personal data
    /// (RNF-13): they leave the API only to the private screen, and they stay out of any
    /// export.
    /// </summary>
    [DebuggerDisplay("IdVehicle={IdVehicle}, Amount={Amount}, Date={Date}")]
    public class Sale : VehicleEntity
    {
        private Sale() { }

        /// <summary>The proposal this sale closed, when it came from one.</summary>
        public int? IdProposal { get; private set; }

        public DateOnly Date { get; private set; }

        /// <summary>The closed price: everything the buyer gives, car included.</summary>
        public decimal Amount { get; private set; }

        public PaymentMethod PaymentMethod { get; private set; }

        public SaleChannel Channel { get; private set; }

        public string? PartnerStoreName { get; private set; }

        public decimal? PartnerCutPercent { get; private set; }

        /// <summary>
        /// What the store kept, in money. Always filled on a partner sale, because it is the
        /// number that leaves the account — the percentage is how it was agreed, not what was
        /// paid.
        /// </summary>
        public decimal? PartnerCutAmount { get; private set; }

        /// <summary>Paid to a person for bringing the buyer. Zero when nobody was.</summary>
        public decimal Commission { get; private set; }

        public string? CommissionNotes { get; private set; }

        public string BuyerName { get; private set; } = string.Empty;

        /// <summary>CPF or CNPJ, digits only. Personal data.</summary>
        public string? BuyerDocument { get; private set; }

        /// <summary>Digits only. Personal data.</summary>
        public string? BuyerPhone { get; private set; }

        /// <summary>The part of <see cref="Amount"/> that came in as a car. Null without a trade.</summary>
        public decimal? TradeInValue { get; private set; }

        /// <summary>The car that came in, once it exists in stock.</summary>
        public int? IdTradeInVehicle { get; private set; }

        public string? Notes { get; private set; }

        /// <summary>Records a sale.</summary>
        /// <param name="idVehicle">The vehicle sold.</param>
        /// <param name="idProposal">The proposal it closes, if any.</param>
        /// <param name="date">When.</param>
        /// <param name="amount">The closed price, car included when there is a trade.</param>
        /// <param name="paymentMethod">How the buyer paid.</param>
        /// <param name="channel">Direct, or through a partner store.</param>
        /// <param name="partnerStoreName">Which store, when through one.</param>
        /// <param name="partnerCutPercent">The store's percentage, when agreed that way.</param>
        /// <param name="partnerCutAmount">The store's amount, when agreed that way.</param>
        /// <param name="commission">Commission paid to a person, zero when none.</param>
        /// <param name="commissionNotes">To whom, and why.</param>
        /// <param name="buyerName">Who bought.</param>
        /// <param name="buyerDocument">Their CPF or CNPJ.</param>
        /// <param name="buyerPhone">Their phone.</param>
        /// <param name="tradeInValue">What the incoming car was valued at, null without a trade.</param>
        /// <param name="notes">Anything else.</param>
        /// <param name="createdBy">Who recorded it.</param>
        /// <returns>The sale.</returns>
        public static Sale Create(
            int idVehicle,
            int? idProposal,
            DateOnly date,
            decimal amount,
            PaymentMethod paymentMethod,
            SaleChannel channel,
            string? partnerStoreName,
            decimal? partnerCutPercent,
            decimal? partnerCutAmount,
            decimal commission,
            string? commissionNotes,
            string buyerName,
            string? buyerDocument,
            string? buyerPhone,
            decimal? tradeInValue,
            string? notes,
            string createdBy = SystemActor)
        {
            if (amount <= 0)
            {
                throw new BusinessRuleException("Informe o valor da venda.");
            }

            if (commission < 0)
            {
                throw new BusinessRuleException("A comissão é um valor positivo, ou zero.");
            }

            if (string.IsNullOrWhiteSpace(buyerName))
            {
                throw new BusinessRuleException("Informe quem comprou.");
            }

            var document = Digits(buyerDocument);

            if (document is not null && !BrazilianDocuments.IsValidCpfOrCnpj(document))
            {
                throw new BusinessRuleException("Informe um CPF ou CNPJ válido para o comprador.");
            }

            var hasTrade = paymentMethod is PaymentMethod.TradeIn or PaymentMethod.TradeInWithCash;

            if (hasTrade && tradeInValue is not > 0)
            {
                throw new BusinessRuleException("Informe o valor do carro que entrou na troca.");
            }

            if (!hasTrade && tradeInValue is not null)
            {
                throw new BusinessRuleException(
                    "Um valor de troca exige a forma de pagamento com troca.");
            }

            if (tradeInValue > amount)
            {
                throw new BusinessRuleException(
                    "O carro que entrou vale, no máximo, o valor da venda.");
            }

            if (paymentMethod == PaymentMethod.TradeIn && tradeInValue != amount)
            {
                throw new BusinessRuleException(
                    "Numa troca sem dinheiro, o carro que entrou vale o preço da venda. " +
                    "Se houve volta em dinheiro, escolha \"Troca com volta\".");
            }

            var sale = new Sale
            {
                IdVehicle = idVehicle,
                IdProposal = idProposal,
                Date = date,
                Amount = amount,
                PaymentMethod = paymentMethod,
                Commission = commission,
                CommissionNotes = Trim(commissionNotes),
                BuyerName = buyerName.Trim(),
                BuyerDocument = document,
                BuyerPhone = Digits(buyerPhone),
                TradeInValue = tradeInValue,
                Notes = Trim(notes)
            };

            sale.SetChannel(channel, partnerStoreName, partnerCutPercent, partnerCutAmount);
            sale.SetCreatedBy(createdBy);

            return sale;
        }

        /// <summary>The money part of the price: everything that was not the incoming car.</summary>
        public decimal CashAmount => Amount - (TradeInValue ?? 0);

        /// <summary>What was actually left after the deal (RF-21).</summary>
        /// <param name="cost">What the vehicle cost.</param>
        /// <returns>The result. Nothing of it is stored.</returns>
        public DealResult ResultAgainst(VehicleCost cost)
        {
            ArgumentNullException.ThrowIfNull(cost);

            return new DealResult(Amount, PartnerCutAmount ?? 0, Commission, cost.Total);
        }

        /// <summary>Links the car that came in, once it has been registered.</summary>
        /// <param name="idVehicle">The incoming vehicle.</param>
        public void AttachTradeInVehicle(int idVehicle)
        {
            if (TradeInValue is null)
            {
                throw new BusinessRuleException("Esta venda foi fechada sem troca.");
            }

            IdTradeInVehicle = idVehicle;
        }

        private void SetChannel(
            SaleChannel channel,
            string? storeName,
            decimal? percent,
            decimal? fixedAmount)
        {
            Channel = channel;

            if (channel == SaleChannel.Direct)
            {
                PartnerStoreName = null;
                PartnerCutPercent = null;
                PartnerCutAmount = null;
                return;
            }

            if (string.IsNullOrWhiteSpace(storeName))
            {
                throw new BusinessRuleException("Informe a loja parceira.");
            }

            PartnerStoreName = storeName.Trim();
            PartnerCutPercent = percent;

            // The amount is what leaves the account, so it is always resolved and kept.
            PartnerCutAmount = DealResult.PartnerCutOf(Amount, percent, fixedAmount);
        }

        private static string? Trim(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static string? Digits(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var digits = new string([.. value.Where(char.IsDigit)]);

            return digits.Length == 0 ? null : digits;
        }
    }
}
