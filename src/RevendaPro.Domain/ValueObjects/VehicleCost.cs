using RevendaPro.Domain.Entities;

namespace RevendaPro.Domain.ValueObjects
{
    /// <summary>
    /// What a vehicle cost, and what that means for a price.
    ///
    /// <b>Nothing here is stored.</b> Every number is computed from the purchase and the
    /// expenses, on every read.
    ///
    /// The reason is a defect found in the real spending sheet the business keeps today: the
    /// total was typed once, three expenses were added underneath it afterwards, and the
    /// document went on showing <b>R$ 350 less</b> than the car had cost. A stored total is
    /// right until the next expense, and wrong from then on, silently. The business described
    /// the same thing in the interview without noticing it was a defect.
    /// </summary>
    /// <param name="Purchase">What was paid for the vehicle.</param>
    /// <param name="PaidExpenses">Everything already spent on it.</param>
    /// <param name="PlannedExpenses">What is expected and still unpaid (RF-11).</param>
    /// <param name="BudgetCeiling">The most it is meant to cost, when there is a ceiling.</param>
    /// <param name="FipeValue">Reference value, when informed.</param>
    public sealed record VehicleCost(
        decimal Purchase,
        decimal PaidExpenses,
        decimal PlannedExpenses,
        decimal? BudgetCeiling,
        decimal? FipeValue)
    {
        /// <summary>What the vehicle cost so far: purchase plus what was paid (RF-10).</summary>
        public decimal Total => Purchase + PaidExpenses;

        /// <summary>Where the cost lands if everything planned is spent (RF-11).</summary>
        public decimal Projected => Total + PlannedExpenses;

        /// <summary>
        /// How much of the ceiling is gone, from 0 to over 100. Null without a ceiling.
        /// </summary>
        public decimal? BudgetUsedPercent =>
            BudgetCeiling is > 0 ? Round(Total / BudgetCeiling.Value * 100) : null;

        /// <summary>
        /// How much room is left, which is the number somebody buying a part actually needs.
        /// Negative once the ceiling is past.
        /// </summary>
        public decimal? BudgetRemaining => BudgetCeiling is > 0 ? BudgetCeiling - Total : null;

        /// <summary>Whether the vehicle already costs more than it was meant to.</summary>
        public bool IsOverBudget => BudgetRemaining is < 0;

        /// <summary>
        /// Whether the planned expenses take it past the ceiling, even though it fits today.
        /// This is the warning worth giving, because it arrives while there is still a choice.
        /// </summary>
        public bool WillExceedBudget => BudgetCeiling is > 0 && Projected > BudgetCeiling;

        /// <summary>What the cost represents against the reference table (RF-15).</summary>
        public decimal? PercentOfFipe =>
            FipeValue is > 0 ? Round(Total / FipeValue.Value * 100) : null;

        /// <summary>Profit at a given price (RF-17).</summary>
        /// <param name="price">The price under consideration.</param>
        /// <returns>Price minus what the vehicle cost.</returns>
        public decimal ProfitAt(decimal price) => price - Total;

        /// <summary>Margin over the price, as a percentage (RF-17).</summary>
        /// <param name="price">The price under consideration.</param>
        /// <returns>The margin, or null when the price is zero.</returns>
        public decimal? MarginAt(decimal price) =>
            price > 0 ? Round(ProfitAt(price) / price * 100) : null;

        /// <summary>Builds the cost of a vehicle from its expenses.</summary>
        /// <param name="vehicle">The vehicle.</param>
        /// <param name="expenses">Its expenses, paid and planned.</param>
        /// <returns>The cost.</returns>
        public static VehicleCost Of(Vehicle vehicle, IEnumerable<VehicleExpense> expenses)
        {
            ArgumentNullException.ThrowIfNull(vehicle);
            ArgumentNullException.ThrowIfNull(expenses);

            var list = expenses as IReadOnlyCollection<VehicleExpense> ?? [.. expenses];

            return new VehicleCost(
                vehicle.PurchasePrice,
                list.Where(e => e.IsPaid).Sum(e => e.Amount),
                list.Where(e => !e.IsPaid).Sum(e => e.Amount),
                vehicle.BudgetCeiling,
                vehicle.FipeValue);
        }

        /// <summary>
        /// Two decimals, away from zero. Money and percentages are shown to somebody deciding
        /// on a number, so the rounding is the one a person does on paper — and never the
        /// banker's rounding the runtime picks by default.
        /// </summary>
        private static decimal Round(decimal value) =>
            Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }
}
