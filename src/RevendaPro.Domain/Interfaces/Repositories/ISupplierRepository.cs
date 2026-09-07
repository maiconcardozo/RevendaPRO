using Foundation.Domain.Interfaces.Repositories;
using RevendaPro.Domain.Entities;

namespace RevendaPro.Domain.Interfaces.Repositories
{
    /// <summary>
    /// De quem a revenda compra serviço e peça: oficina, funilaria, autopeças, despachante.
    /// </summary>
    public interface ISupplierRepository : IDapperRepository<Supplier>
    {
        /// <summary>Os fornecedores de uma revenda, por nome.</summary>
        /// <param name="idTenant">Empresa dona do cadastro.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>Os fornecedores.</returns>
        Task<IReadOnlyList<Supplier>> ListByTenantAsync(
            int idTenant,
            CancellationToken cancellationToken = default);

        /// <summary>Acha um fornecedor pelo código público.</summary>
        /// <param name="idTenant">Empresa dona do cadastro.</param>
        /// <param name="code">Identificador público.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>O fornecedor, ou nulo.</returns>
        Task<Supplier?> GetByCodeAsync(
            int idTenant,
            Guid code,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Quantos gastos apontam para um fornecedor.
        ///
        /// Existe para a exclusão recusar com um motivo, em vez de deixar gasto apontando para
        /// alguém que sumiu — a mesma rede do tipo de gasto e do pátio.
        /// </summary>
        /// <param name="idSupplier">O fornecedor.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>Quantos gastos apontam para ele.</returns>
        Task<int> CountExpensesAsync(int idSupplier, CancellationToken cancellationToken = default);

        /// <summary>Se a revenda já tem um fornecedor com esse nome.</summary>
        /// <param name="idTenant">Empresa dona do cadastro.</param>
        /// <param name="name">O nome.</param>
        /// <param name="ignoreId">Fornecedor a deixar de fora, ao editar.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>Verdadeiro quando o nome já está em uso.</returns>
        Task<bool> NameExistsAsync(
            int idTenant,
            string name,
            int? ignoreId,
            CancellationToken cancellationToken = default);
    }

    /// <summary>Os ramos de fornecedor de uma revenda.</summary>
    public interface ISupplierSegmentRepository : IDapperRepository<SupplierSegment>
    {
        /// <summary>Os ramos de uma revenda, na ordem em que ela os mostra.</summary>
        /// <param name="idTenant">Empresa dona do cadastro.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>Os ramos.</returns>
        Task<IReadOnlyList<SupplierSegment>> ListByTenantAsync(
            int idTenant,
            CancellationToken cancellationToken = default);

        /// <summary>Acha um ramo pelo código público.</summary>
        /// <param name="idTenant">Empresa dona do cadastro.</param>
        /// <param name="code">Identificador público.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>O ramo, ou nulo.</returns>
        Task<SupplierSegment?> GetByCodeAsync(
            int idTenant,
            Guid code,
            CancellationToken cancellationToken = default);

        /// <summary>Quantos fornecedores estão num ramo, para a exclusão recusar com um motivo.</summary>
        /// <param name="idSupplierSegment">O ramo.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>Quantos fornecedores estão nele.</returns>
        Task<int> CountSuppliersAsync(int idSupplierSegment, CancellationToken cancellationToken = default);

        /// <summary>Se a revenda já tem um ramo com esse nome.</summary>
        /// <param name="idTenant">Empresa dona do cadastro.</param>
        /// <param name="name">O nome.</param>
        /// <param name="ignoreId">Ramo a deixar de fora, ao editar.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>Verdadeiro quando o nome já está em uso.</returns>
        Task<bool> NameExistsAsync(
            int idTenant,
            string name,
            int? ignoreId,
            CancellationToken cancellationToken = default);
    }
}
