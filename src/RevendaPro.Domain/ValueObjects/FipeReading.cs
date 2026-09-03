namespace RevendaPro.Domain.ValueObjects
{
    /// <summary>What happened when the reference table was asked.</summary>
    public enum FipeOutcome
    {
        /// <summary>The table answered.</summary>
        Found = 1,

        /// <summary>
        /// The table answered, and does not have what was asked. A car outside the table is a
        /// real case — imported, very old, or a model the table never priced.
        /// </summary>
        Missing = 2,

        /// <summary>
        /// The table could not be reached, or answered with a failure. Says nothing about the
        /// vehicle: the operation keeps the last known value and moves on.
        /// </summary>
        Unavailable = 3
    }

    /// <summary>
    /// A reading of the reference table.
    ///
    /// Exists instead of a nullable return so that "the table does not have this car" and "the
    /// table is out of reach" stay apart. They read the same on screen if collapsed, and they
    /// are opposite facts: the first is final, and the second is worth trying again in an hour.
    /// </summary>
    /// <typeparam name="T">What was read.</typeparam>
    /// <param name="Outcome">What happened.</param>
    /// <param name="Value">What was read, when it was read.</param>
    /// <param name="Detail">Technical reason, for the log. Never shown to the user as is.</param>
    public sealed record FipeResult<T>(FipeOutcome Outcome, T? Value, string? Detail)
    {
        /// <summary>Whether there is a value to use.</summary>
        public bool Ok => Outcome == FipeOutcome.Found && Value is not null;

        /// <summary>The table answered with a value.</summary>
        /// <param name="value">What it answered.</param>
        /// <returns>The reading.</returns>
        public static FipeResult<T> Found(T value) => new(FipeOutcome.Found, value, null);

        /// <summary>The table does not have what was asked.</summary>
        /// <param name="detail">Technical reason.</param>
        /// <returns>The reading.</returns>
        public static FipeResult<T> Missing(string? detail = null) =>
            new(FipeOutcome.Missing, default, detail);

        /// <summary>The table could not be reached.</summary>
        /// <param name="detail">Technical reason, for the log.</param>
        /// <returns>The reading.</returns>
        public static FipeResult<T> Unavailable(string detail) =>
            new(FipeOutcome.Unavailable, default, detail);
    }

    /// <summary>
    /// One published table.
    /// </summary>
    /// <param name="Code">Identifier the source uses to pin a query (<c>337</c>).</param>
    /// <param name="Month">
    /// First day of the month it refers to. A table is monthly, so the day carries no meaning
    /// and is always the first — which makes two readings of the same month comparable.
    /// </param>
    public sealed record FipeReference(int Code, DateOnly Month);

    /// <summary>
    /// The price of one model in one table.
    /// </summary>
    /// <param name="FipeCode">Code of the model, as printed by the table.</param>
    /// <param name="YearFuel">Year and fuel of the exact priced row.</param>
    /// <param name="Reference">Which month this price belongs to.</param>
    /// <param name="Value">
    /// The price, in decimal. The source sends it as text in Brazilian format
    /// (<c>"R$ 56.530,00"</c>); turning that into a binary floating point number would lose
    /// cents, and money in this system is decimal (RNF-12).
    /// </param>
    /// <param name="Brand">Brand as the table writes it (<c>GM - Chevrolet</c>).</param>
    /// <param name="Model">Model as the table writes it, version included.</param>
    /// <param name="ModelYear">Model year.</param>
    /// <param name="Fuel">Fuel as the table writes it.</param>
    public sealed record FipePrice(
        string FipeCode,
        string YearFuel,
        DateOnly Reference,
        decimal Value,
        string Brand,
        string Model,
        short ModelYear,
        string Fuel);

    /// <summary>
    /// One year and fuel combination of a model.
    /// </summary>
    /// <param name="YearFuel">What the source expects back (<c>2014-5</c>).</param>
    /// <param name="Name">What a person reads (<c>2014 Flex</c>).</param>
    /// <param name="ModelYear">The year alone, for matching a vehicle already registered.</param>
    public sealed record FipeYearOption(string YearFuel, string Name, short ModelYear);
}
