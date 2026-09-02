using Foundation.Domain.Interfaces.Repositories;
using RevendaPro.Domain.Entities;

namespace RevendaPro.Domain.Interfaces.Repositories
{
    /// <summary>Proposals received for a vehicle (RF-18).</summary>
    public interface IProposalRepository : IDapperRepository<Proposal>
    {
        /// <summary>Proposals of one vehicle, newest first.</summary>
        /// <param name="idVehicle">The vehicle.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The proposals, every status.</returns>
        Task<IReadOnlyList<Proposal>> ListByVehicleAsync(
            int idVehicle,
            CancellationToken cancellationToken = default);

        /// <summary>Finds a proposal by its public code.</summary>
        /// <param name="code">Public identifier.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The proposal, or null.</returns>
        /// <remarks>Hides the base declaration on purpose. See <see cref="IVehicleExpenseRepository"/>.</remarks>
        new Task<Proposal?> GetByCodeAsync(Guid code, CancellationToken cancellationToken = default);
    }

    /// <summary>Sales (RF-20).</summary>
    public interface ISaleRepository : IDapperRepository<Sale>
    {
        /// <summary>The active sale of a vehicle, if it was sold.</summary>
        /// <param name="idVehicle">The vehicle.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The sale, or null while the car is on the lot.</returns>
        Task<Sale?> GetByVehicleAsync(int idVehicle, CancellationToken cancellationToken = default);

        /// <summary>Finds a sale by its public code.</summary>
        /// <param name="code">Public identifier.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The sale, or null.</returns>
        /// <remarks>Hides the base declaration on purpose. See <see cref="IVehicleExpenseRepository"/>.</remarks>
        new Task<Sale?> GetByCodeAsync(Guid code, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sales of a tenant inside a period, newest first. The tenant comes through the
        /// vehicle, as with every other row that belongs to one.
        /// </summary>
        /// <param name="idTenant">Owning tenant.</param>
        /// <param name="from">First day, inclusive. Null for no lower bound.</param>
        /// <param name="to">Last day, inclusive. Null for no upper bound.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The sales.</returns>
        Task<IReadOnlyList<Sale>> ListByTenantAsync(
            int idTenant,
            DateOnly? from,
            DateOnly? to,
            CancellationToken cancellationToken = default);
    }
}
