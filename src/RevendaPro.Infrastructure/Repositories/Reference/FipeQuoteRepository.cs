using Foundation.Dapper.Repositories;
using Foundation.Domain.Interfaces.UnitOfWork;
using RevendaPro.Domain.Entities;
using RevendaPro.Domain.Interfaces.Repositories;
using RevendaPro.Infrastructure.Queries.Reference;

namespace RevendaPro.Infrastructure.Repositories.Reference
{
    /// <summary>Dapper repository for <see cref="FipeQuote"/>.</summary>
    public class FipeQuoteRepository(IDapperUnitOfWork unitOfWork)
        : DapperRepository<FipeQuote>(unitOfWork), IFipeQuoteRepository
    {
        /// <inheritdoc/>
        public Task<FipeQuote?> FindAsync(
            string fipeCode,
            string yearFuel,
            DateOnly referenceMonth,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(fipeCode);
            ArgumentException.ThrowIfNullOrWhiteSpace(yearFuel);

            return QuerySingleAsync(
                new FindFipeQuoteQuery(fipeCode.Trim(), yearFuel.Trim(), referenceMonth),
                cancellationToken);
        }

        /// <inheritdoc/>
        public Task<IReadOnlyList<FipeQuote>> ListByModelAsync(
            string fipeCode,
            string yearFuel,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(fipeCode);
            ArgumentException.ThrowIfNullOrWhiteSpace(yearFuel);

            return QueryAsync(
                new ListFipeQuotesByModelQuery(fipeCode.Trim(), yearFuel.Trim()),
                cancellationToken);
        }
    }
}
