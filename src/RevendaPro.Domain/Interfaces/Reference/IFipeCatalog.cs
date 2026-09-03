using RevendaPro.Domain.ValueObjects;

namespace RevendaPro.Domain.Interfaces.Reference
{
    /// <summary>
    /// The reference table of vehicle prices, as the domain asks for it.
    ///
    /// FIPE publishes no API: the official access is a web page, one model at a time. What
    /// exists are third party mirrors, and any of them can vanish, change shape or start
    /// charging. So the domain states what it needs and the infrastructure answers — the same
    /// shape as file storage in ADR-0004. Swapping the source is a new adapter, and nothing
    /// else in the system finds out. See ADR-0005.
    ///
    /// Nothing here throws for a source problem. A table that fails to answer is a normal
    /// state of the world, not a defect: the value is a <b>reference</b>, and the price is
    /// decided by a person. Every method says what happened through
    /// <see cref="FipeResult{T}"/>, and the caller decides — which in practice means keeping
    /// the last known value and marking it as old.
    /// </summary>
    public interface IFipeCatalog
    {
        /// <summary>
        /// The most recent published table.
        ///
        /// Asked first, always, and every other call is pinned to what comes back. The reason
        /// is a real observation: two calls to the same mirror, in the same minute, answered
        /// with different months — August for one path and September for another. Pinning the
        /// reference is what makes a quote reproducible.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The newest reference, or why it could not be read.</returns>
        Task<FipeResult<FipeReference>> GetCurrentReferenceAsync(
            CancellationToken cancellationToken = default);

        /// <summary>
        /// The price of one exact model, in one exact table.
        /// </summary>
        /// <param name="fipeCode">Code of the model, as printed by the table (<c>004380-0</c>).</param>
        /// <param name="yearFuel">
        /// Year and fuel of the exact row (<c>2014-5</c>). A model year alone is ambiguous: the
        /// same car and year exist as flex and as gasoline, at different prices.
        /// </param>
        /// <param name="reference">The table to read, from <see cref="GetCurrentReferenceAsync"/>.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The price, or why it could not be read.</returns>
        Task<FipeResult<FipePrice>> GetPriceAsync(
            string fipeCode,
            string yearFuel,
            int reference,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Which year and fuel combinations exist for a code, so a model year can be turned
        /// into the exact row the table prices.
        /// </summary>
        /// <param name="fipeCode">Code of the model.</param>
        /// <param name="reference">The table to read.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The options, or why they could not be read.</returns>
        Task<FipeResult<IReadOnlyList<FipeYearOption>>> ListYearsAsync(
            string fipeCode,
            int reference,
            CancellationToken cancellationToken = default);
    }
}
