using Foundation.Domain.Interfaces.Repositories;
using RevendaPro.Domain.Entities;

namespace RevendaPro.Domain.Interfaces.Repositories
{
    /// <summary>
    /// Os lugares onde os carros da revenda ficam: o pátio dela, e as lojas de terceiros onde
    /// ela deixou carro para vender.
    /// </summary>
    public interface IYardRepository : IDapperRepository<Yard>
    {
        /// <summary>Os pátios de uma revenda, na ordem em que ela os mostra.</summary>
        /// <param name="idTenant">Empresa dona do cadastro.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>Os pátios.</returns>
        Task<IReadOnlyList<Yard>> ListByTenantAsync(
            int idTenant,
            CancellationToken cancellationToken = default);

        /// <summary>Acha um pátio pelo código público.</summary>
        /// <param name="idTenant">Empresa dona do cadastro.</param>
        /// <param name="code">Identificador público.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>O pátio, ou nulo.</returns>
        Task<Yard?> GetByCodeAsync(
            int idTenant,
            Guid code,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Quantos carros estão num pátio.
        ///
        /// Existe para a exclusão recusar com um motivo, em vez de deixar carro apontando para
        /// um lugar que sumiu — a mesma rede que o tipo de gasto já tem desde o M6.
        /// </summary>
        /// <param name="idYard">O pátio.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>Quantos carros estão nele.</returns>
        Task<int> CountVehiclesAsync(int idYard, CancellationToken cancellationToken = default);

        /// <summary>Se a revenda já tem um pátio com esse nome.</summary>
        /// <param name="idTenant">Empresa dona do cadastro.</param>
        /// <param name="name">O nome.</param>
        /// <param name="ignoreId">Pátio a deixar de fora, ao editar.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>Verdadeiro quando o nome já está em uso.</returns>
        Task<bool> NameExistsAsync(
            int idTenant,
            string name,
            int? ignoreId,
            CancellationToken cancellationToken = default);
    }
}
