using RevendaPro.Domain.Enums;

namespace RevendaPro.Domain.ValueObjects
{
    /// <summary>
    /// One amount against the reference table of the month it belongs to.
    ///
    /// The month is the whole point. Comparing a sale closed in August against the table of
    /// today would measure the passage of time and call it a result — and time is exactly what
    /// this screen is trying to measure separately.
    /// </summary>
    /// <param name="Amount">What was actually paid, offered or asked.</param>
    /// <param name="Reference">
    /// What the table of that month said, or null when that month was never fetched. The free
    /// tier of the source gives three months of history and the system only started keeping
    /// quotes at the M11, so a deal older than that has no comparison — and the screen says so
    /// instead of inventing a number.
    /// </param>
    public sealed record MarketComparison(decimal Amount, decimal? Reference)
    {
        /// <summary>Whether there is a table to compare against.</summary>
        public bool HasReference => Reference is > 0;

        /// <summary>How many reais above the table. Negative below it.</summary>
        public decimal? Difference => HasReference ? Amount - Reference!.Value : null;

        /// <summary>The same distance as a percentage of the table.</summary>
        public decimal? Percent =>
            HasReference ? Round((Amount - Reference!.Value) / Reference.Value * 100) : null;

        /// <summary>Whether the amount came in above the table.</summary>
        public bool Above => Difference is > 0;

        private static decimal Round(decimal value) =>
            Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Where one vehicle stands against the reference table, at every moment that matters.
    ///
    /// <b>Nothing here is stored.</b> Each comparison is the amount of the deal next to the
    /// quote of that deal's month, and the quotes are the historical fact the M11 keeps. Same
    /// reasoning as <see cref="VehicleCost"/>: a stored comparison is right until the table
    /// moves, and wrong from then on.
    /// </summary>
    /// <param name="Code">Public identifier of the vehicle.</param>
    /// <param name="Plate">Plate.</param>
    /// <param name="Brand">Brand.</param>
    /// <param name="Model">Model.</param>
    /// <param name="Version">Version or trim.</param>
    /// <param name="ModelYear">Model year.</param>
    /// <param name="Status">Where it is in the pipeline.</param>
    /// <param name="DaysInStock">How long it has been on the lot, or was.</param>
    /// <param name="PurchasePrice">What was paid for it.</param>
    /// <param name="PurchaseDate">When it was bought.</param>
    /// <param name="PurchaseReference">The table of the month it was bought.</param>
    /// <param name="DesiredNetPrice">What the dealership wants to take home.</param>
    /// <param name="CurrentReference">The table of this month.</param>
    /// <param name="PreviousReference">The table of last month, for what the month cost.</param>
    /// <param name="SaleAmount">What it closed for, when it is sold.</param>
    /// <param name="SaleDate">When it closed.</param>
    /// <param name="SaleReference">The table of the month it closed.</param>
    public sealed record MarketPosition(
        Guid Code,
        string Plate,
        string Brand,
        string Model,
        string? Version,
        short ModelYear,
        VehicleStatus Status,
        int? DaysInStock,
        decimal PurchasePrice,
        DateOnly? PurchaseDate,
        decimal? PurchaseReference,
        decimal? DesiredNetPrice,
        decimal? CurrentReference,
        decimal? PreviousReference,
        decimal? SaleAmount,
        DateOnly? SaleDate,
        decimal? SaleReference)
    {
        /// <summary>Whether the car is still on the lot.</summary>
        public bool OnTheLot => Status != VehicleStatus.Sold;

        /// <summary>
        /// What was paid against the table of that month — the advantage of an auction,
        /// measured instead of assumed.
        /// </summary>
        public MarketComparison? Purchase =>
            PurchasePrice > 0 ? new MarketComparison(PurchasePrice, PurchaseReference) : null;

        /// <summary>What it closed for against the table of the month it closed.</summary>
        public MarketComparison? Sale =>
            SaleAmount is > 0 ? new MarketComparison(SaleAmount.Value, SaleReference) : null;

        /// <summary>What the dealership is asking against the table of today.</summary>
        public MarketComparison? Asking =>
            DesiredNetPrice is > 0 ? new MarketComparison(DesiredNetPrice.Value, CurrentReference) : null;

        /// <summary>
        /// How much reference the car lost since it was bought — the cost of holding it,
        /// which until now nobody could name.
        /// </summary>
        public decimal? LostSincePurchase =>
            OnTheLot && PurchaseReference is > 0 && CurrentReference is > 0
                ? PurchaseReference.Value - CurrentReference.Value
                : null;

        /// <summary>How much reference it lost in this month alone.</summary>
        public decimal? LostThisMonth =>
            OnTheLot && PreviousReference is > 0 && CurrentReference is > 0
                ? PreviousReference.Value - CurrentReference.Value
                : null;
    }

    /// <summary>
    /// An offer on the table, against the table of this month.
    /// </summary>
    /// <param name="VehicleCode">Public identifier of the vehicle.</param>
    /// <param name="Plate">Plate.</param>
    /// <param name="Brand">Brand.</param>
    /// <param name="Model">Model.</param>
    /// <param name="ProspectName">Who offered.</param>
    /// <param name="Amount">What was offered.</param>
    /// <param name="Date">When.</param>
    /// <param name="CurrentReference">The table of this month.</param>
    public sealed record MarketProposal(
        Guid VehicleCode,
        string Plate,
        string Brand,
        string Model,
        string ProspectName,
        decimal Amount,
        DateOnly Date,
        decimal? CurrentReference)
    {
        /// <summary>The offer against the table of today.</summary>
        public MarketComparison Offer => new(Amount, CurrentReference);
    }
}
