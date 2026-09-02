using RevendaPro.Shared.Exceptions;

namespace RevendaPro.Domain.ValueObjects
{
    /// <summary>
    /// What a deal leaves in the seller's hand, for a proposal being weighed or a sale already
    /// closed. The same arithmetic in both places, so the number promised before never
    /// disagrees with the number reported after.
    ///
    /// <b>Nothing here is stored.</b> A stored profit is right until the next expense lands on
    /// the car, and wrong from then on — the same defect the cost sheet had.
    /// </summary>
    /// <param name="Amount">The price the buyer pays.</param>
    /// <param name="PartnerCut">What the partner store keeps, zero on a direct deal.</param>
    /// <param name="Commission">Commission paid to a person, zero when there is none.</param>
    /// <param name="Cost">What the vehicle cost, purchase plus what was paid on it.</param>
    public sealed record DealResult(
        decimal Amount,
        decimal PartnerCut,
        decimal Commission,
        decimal Cost)
    {
        /// <summary>What reaches the seller once the store keeps its cut.</summary>
        public decimal Received => Amount - PartnerCut;

        /// <summary>Price minus cost, before anybody is paid (RF-21).</summary>
        public decimal GrossProfit => Amount - Cost;

        /// <summary>What is actually left: received, minus commission, minus cost (RF-19, RF-21).</summary>
        public decimal NetProfit => Received - Commission - Cost;

        /// <summary>Net profit over the price, as a percentage. Null when the price is zero.</summary>
        public decimal? Margin => Amount > 0 ? Round(NetProfit / Amount * 100) : null;

        /// <summary>
        /// Resolves the partner cut from whichever the store gave: a percentage of the price,
        /// or a fixed amount. The business does not know yet which one its stores use, so both
        /// are accepted, and the one not given is derived on screen.
        /// </summary>
        /// <param name="amount">The price the buyer pays.</param>
        /// <param name="percent">The store's percentage, when that is what was agreed.</param>
        /// <param name="fixedAmount">The store's fixed amount, when that is what was agreed.</param>
        /// <returns>The cut in money, two decimals.</returns>
        public static decimal PartnerCutOf(decimal amount, decimal? percent, decimal? fixedAmount)
        {
            if (percent is not null && fixedAmount is not null)
            {
                throw new BusinessRuleException(
                    "Informe o repasse da loja em percentual ou em valor, e apenas um dos dois.");
            }

            if (percent is < 0 or > 100)
            {
                throw new BusinessRuleException("O percentual da loja fica entre 0 e 100.");
            }

            if (fixedAmount is < 0)
            {
                throw new BusinessRuleException("O repasse da loja é um valor positivo.");
            }

            if (fixedAmount > amount)
            {
                throw new BusinessRuleException("O repasse da loja é menor que o preço da venda.");
            }

            return fixedAmount ?? (percent is null ? 0 : Round(amount * percent.Value / 100));
        }

        /// <summary>Two decimals, away from zero — the rounding a person does on paper.</summary>
        private static decimal Round(decimal value) =>
            Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }
}
