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

        /// <summary>
        /// Quanto foi para cada fornecedor da revenda, somado pelo banco.
        ///
        /// Uma linha por fornecedor com gasto no período, e nenhuma para quem ficou sem. A soma
        /// é do banco, e não da lista de gastos carregada para somar aqui — que é o que a
        /// listagem recusa desde o M6. Passa pelo veículo para chegar à revenda, porque o gasto
        /// não carrega <c>IdTenant</c> de propósito.
        /// </summary>
        /// <param name="idTenant">Empresa dona do cadastro.</param>
        /// <param name="from">Primeiro dia, inclusive. Nulo para sem limite.</param>
        /// <param name="to">Último dia, inclusive. Nulo para sem limite.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>O gasto por fornecedor.</returns>
        Task<IReadOnlyList<SupplierSpend>> SumByTenantAsync(
            int idTenant,
            DateOnly? from,
            DateOnly? to,
            CancellationToken cancellationToken = default);

        /// <summary>Os gastos de um fornecedor, com o carro de cada um, do mais recente para o mais antigo.</summary>
        /// <param name="idTenant">Empresa dona do cadastro.</param>
        /// <param name="idSupplier">O fornecedor.</param>
        /// <param name="from">Primeiro dia, inclusive. Nulo para sem limite.</param>
        /// <param name="to">Último dia, inclusive. Nulo para sem limite.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>As linhas.</returns>
        Task<IReadOnlyList<SupplierExpenseLine>> ListExpensesAsync(
            int idTenant,
            int idSupplier,
            DateOnly? from,
            DateOnly? to,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// As estatísticas do gasto com fornecedores num período: os totais, a soma por ramo,
        /// por tipo de gasto e por mês. Quatro consultas com <c>GROUP BY</c>, e nenhuma linha de
        /// gasto carregada para somar aqui.
        /// </summary>
        /// <param name="idTenant">Empresa dona do cadastro.</param>
        /// <param name="from">Primeiro dia, inclusive. Nulo para sem limite.</param>
        /// <param name="to">Último dia, inclusive. Nulo para sem limite.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>As estatísticas.</returns>
        Task<SupplierStatistics> ReadStatisticsAsync(
            int idTenant,
            DateOnly? from,
            DateOnly? to,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Só a soma por mês, para o dashboard mostrar a tendência de doze meses enquanto o resto
        /// do painel obedece ao período das vendas.
        /// </summary>
        /// <param name="idTenant">Empresa dona do cadastro.</param>
        /// <param name="from">Primeiro dia, inclusive. Nulo para sem limite.</param>
        /// <param name="to">Último dia, inclusive. Nulo para sem limite.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A soma por mês, com a chave ano × 100 + mês.</returns>
        Task<IReadOnlyList<SpendSlice>> SumByMonthAsync(
            int idTenant,
            DateOnly? from,
            DateOnly? to,
            CancellationToken cancellationToken = default);
    }

    /// <summary>Uma linha de soma por chave: ramo, tipo de gasto ou mês.</summary>
    /// <param name="Key">O Id do ramo ou do tipo; para o mês, ano × 100 + mês.</param>
    /// <param name="PaidTotal">O que foi pago.</param>
    /// <param name="PlannedTotal">O que está previsto.</param>
    /// <param name="ExpenseCount">Quantos gastos.</param>
    public sealed record SpendSlice(int Key, decimal PaidTotal, decimal PlannedTotal, int ExpenseCount);

    /// <summary>As estatísticas do gasto com fornecedores num período.</summary>
    /// <param name="PaidTotal">O que foi pago a fornecedores.</param>
    /// <param name="PlannedTotal">O que está previsto com fornecedores.</param>
    /// <param name="ExpenseCount">Quantos gastos com fornecedor.</param>
    /// <param name="VehicleCount">Em quantos carros.</param>
    /// <param name="UnassignedPaid">O que foi pago sem fornecedor no gasto (taxa, multa, ou esquecimento).</param>
    /// <param name="BySegment">A soma por ramo.</param>
    /// <param name="ByType">A soma por tipo de gasto.</param>
    /// <param name="ByMonth">A soma por mês, com a chave ano × 100 + mês.</param>
    public sealed record SupplierStatistics(
        decimal PaidTotal,
        decimal PlannedTotal,
        int ExpenseCount,
        int VehicleCount,
        decimal UnassignedPaid,
        IReadOnlyList<SpendSlice> BySegment,
        IReadOnlyList<SpendSlice> ByType,
        IReadOnlyList<SpendSlice> ByMonth);

    /// <summary>Quanto foi para um fornecedor num período.</summary>
    /// <param name="IdSupplier">O fornecedor.</param>
    /// <param name="PaidTotal">O que foi pago. É o "quanto gastei".</param>
    /// <param name="PlannedTotal">O que está previsto e ainda fora do custo real (RF-11).</param>
    /// <param name="ExpenseCount">Quantos gastos, pagos e previstos.</param>
    /// <param name="LastDate">A data do gasto mais recente.</param>
    public sealed record SupplierSpend(
        int IdSupplier,
        decimal PaidTotal,
        decimal PlannedTotal,
        int ExpenseCount,
        DateOnly? LastDate);

    /// <summary>Um gasto de um fornecedor, com o carro em que ele foi feito.</summary>
    /// <param name="Code">Identificador público do gasto.</param>
    /// <param name="Date">Quando.</param>
    /// <param name="Description">O que foi.</param>
    /// <param name="Amount">Quanto.</param>
    /// <param name="IsPaid">Pago ou previsto.</param>
    /// <param name="IdExpenseType">O tipo, para a quebra por tipo.</param>
    /// <param name="VehicleCode">Identificador público do carro.</param>
    /// <param name="Plate">A placa.</param>
    /// <param name="Brand">A marca.</param>
    /// <param name="Model">O modelo.</param>
    /// <param name="Version">A versão, quando cadastrada.</param>
    /// <param name="ModelYear">O ano do modelo.</param>
    public sealed record SupplierExpenseLine(
        Guid Code,
        DateOnly Date,
        string Description,
        decimal Amount,
        bool IsPaid,
        int IdExpenseType,
        Guid VehicleCode,
        string Plate,
        string Brand,
        string Model,
        string? Version,
        short ModelYear);

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
