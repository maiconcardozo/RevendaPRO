using Foundation.Domain.Interfaces.Repositories;
using RevendaPro.Domain.Entities;

namespace RevendaPro.Domain.Interfaces.Repositories
{
    /// <summary>
    /// What the reference table already said, kept so it is never asked twice.
    ///
    /// There is no tenant here on purpose: a quote is public reference data. See ADR-0005.
    /// </summary>
    public interface IFipeQuoteRepository : IDapperRepository<FipeQuote>
    {
        /// <summary>The quote of one model in one month, when it was already kept.</summary>
        /// <param name="fipeCode">Code of the model in the table.</param>
        /// <param name="yearFuel">Year and fuel of the priced row.</param>
        /// <param name="referenceMonth">Month asked for; the day is ignored.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The quote, or null while that month was never fetched.</returns>
        Task<FipeQuote?> FindAsync(
            string fipeCode,
            string yearFuel,
            DateOnly referenceMonth,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Every month already kept for one model, newest first.
        ///
        /// This is the history of the table inside the system, and it comes for free: it is
        /// what answers how much a parked car loses in reference every month.
        /// </summary>
        /// <param name="fipeCode">Code of the model in the table.</param>
        /// <param name="yearFuel">Year and fuel of the priced row.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The quotes, newest month first.</returns>
        Task<IReadOnlyList<FipeQuote>> ListByModelAsync(
            string fipeCode,
            string yearFuel,
            CancellationToken cancellationToken = default);
    }
}
