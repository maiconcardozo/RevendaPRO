using Foundation.Dapper.Repositories;
using Foundation.Domain.Interfaces.UnitOfWork;
using RevendaPro.Domain.Entities;
using RevendaPro.Domain.Interfaces.Repositories;
using RevendaPro.Infrastructure.Queries.Sales;

namespace RevendaPro.Infrastructure.Repositories.Sales
{
    /// <summary>Dapper repository for <see cref="Proposal"/>.</summary>
    public class ProposalRepository(IDapperUnitOfWork unitOfWork)
        : DapperRepository<Proposal>(unitOfWork), IProposalRepository
    {
        /// <inheritdoc/>
        public Task<IReadOnlyList<Proposal>> ListByVehicleAsync(
            int idVehicle,
            CancellationToken cancellationToken = default) =>
            QueryAsync(new ListProposalsByVehicleQuery(idVehicle), cancellationToken);

        /// <inheritdoc/>
        ///
        /// <remarks>
        /// Hides the base <c>GetByCodeAsync</c> on purpose: reading goes through a query
        /// object, which is where <c>SoftDeleteTests</c> checks that every SELECT carries
        /// <c>IsActive = 1</c>. See ADR-0003.
        /// </remarks>
        public new Task<Proposal?> GetByCodeAsync(
            Guid code,
            CancellationToken cancellationToken = default) =>
            QuerySingleAsync(new FindProposalByCodeQuery(code), cancellationToken);
    }

    /// <summary>Dapper repository for <see cref="Sale"/>.</summary>
    public class SaleRepository(IDapperUnitOfWork unitOfWork)
        : DapperRepository<Sale>(unitOfWork), ISaleRepository
    {
        /// <inheritdoc/>
        public Task<Sale?> GetByVehicleAsync(
            int idVehicle,
            CancellationToken cancellationToken = default) =>
            QuerySingleAsync(new FindSaleByVehicleQuery(idVehicle), cancellationToken);

        /// <inheritdoc/>
        ///
        /// <remarks>Hides the base declaration on purpose. See <see cref="ProposalRepository"/>.</remarks>
        public new Task<Sale?> GetByCodeAsync(
            Guid code,
            CancellationToken cancellationToken = default) =>
            QuerySingleAsync(new FindSaleByCodeQuery(code), cancellationToken);

        /// <inheritdoc/>
        public Task<IReadOnlyList<Sale>> ListByTenantAsync(
            int idTenant,
            DateOnly? from,
            DateOnly? to,
            CancellationToken cancellationToken = default) =>
            QueryAsync(new ListSalesByTenantQuery(idTenant, from, to), cancellationToken);
    }
}
