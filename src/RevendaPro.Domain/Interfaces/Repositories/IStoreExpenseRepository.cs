using Foundation.Domain.Interfaces.Repositories;
using RevendaPro.Domain.Entities;

namespace RevendaPro.Domain.Interfaces.Repositories
{
    /// <summary>
    /// O que a loja paga e que jamais pertence a um carro: aluguel, energia, salário (M22).
    /// </summary>
    public interface IStoreExpenseRepository : IDapperRepository<StoreExpense>
    {
        /// <summary>
        /// As despesas da loja num período, da mais recente para a mais antiga.
        ///
        /// O período é lido sobre a <b>data da despesa</b> — o mês a que ela pertence —, e não
        /// sobre o vencimento: a pergunta desta tela é "o que a loja gastou em setembro", e a
        /// pergunta do vencimento é a do caixa.
        /// </summary>
        /// <param name="idTenant">Revenda dona da despesa.</param>
        /// <param name="from">Primeiro dia, inclusive. Nulo para sem limite.</param>
        /// <param name="to">Último dia, inclusive. Nulo para sem limite.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>As despesas.</returns>
        Task<IReadOnlyList<StoreExpense>> ListByTenantAsync(
            int idTenant,
            DateOnly? from,
            DateOnly? to,
            CancellationToken cancellationToken = default);

        /// <summary>Acha uma despesa da loja pelo código público.</summary>
        /// <param name="idTenant">Revenda dona da despesa.</param>
        /// <param name="code">Identificador público.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A despesa, ou nulo.</returns>
        Task<StoreExpense?> GetByCodeAsync(
            int idTenant,
            Guid code,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Quantas despesas da loja apontam para um tipo de gasto.
        ///
        /// Existe para a exclusão de um tipo recusar com um motivo: desde o M22 um tipo pode
        /// estar em uso dos dois lados, e contar só os gastos de carro deixaria apagar o tipo
        /// Aluguel com doze aluguéis apontando para ele.
        /// </summary>
        /// <param name="idExpenseType">O tipo.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>Quantas despesas apontam para ele.</returns>
        Task<int> CountByExpenseTypeAsync(int idExpenseType, CancellationToken cancellationToken = default);

        /// <summary>
        /// Quantas despesas da loja apontam para um fornecedor. Mesmo motivo do tipo: a
        /// exclusão do fornecedor precisa enxergar os dois lados.
        /// </summary>
        /// <param name="idSupplier">O fornecedor.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>Quantas despesas apontam para ele.</returns>
        Task<int> CountBySupplierAsync(int idSupplier, CancellationToken cancellationToken = default);
    }
}
