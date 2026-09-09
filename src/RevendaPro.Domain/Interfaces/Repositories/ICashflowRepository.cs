using RevendaPro.Domain.Enums;

namespace RevendaPro.Domain.Interfaces.Repositories
{
    /// <summary>
    /// O dinheiro no tempo (M22): o que vence, o que entrou, o que atrasou.
    ///
    /// Não é um repositório de entidade: é a leitura que junta as três origens do caixa — o
    /// gasto do carro, a despesa da loja e o que falta receber de cada venda. Cada uma mora na
    /// sua tabela, com o seu dono e a sua regra; aqui elas viram linhas de um extrato, somadas
    /// pelo banco. Nenhuma lista é carregada para somar em memória.
    /// </summary>
    public interface ICashflowRepository
    {
        /// <summary>
        /// Os números do topo da tela: o que a revenda deve, o que tem a receber, o que já
        /// venceu dos dois lados, e quanto saiu e entrou no período.
        /// </summary>
        /// <param name="idTenant">Revenda.</param>
        /// <param name="from">Primeiro dia do período, inclusive. Nulo para sem limite.</param>
        /// <param name="to">Último dia do período, inclusive. Nulo para sem limite.</param>
        /// <param name="today">O dia de hoje, para decidir o que está vencido.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>Os totais.</returns>
        Task<CashflowSummary> ReadSummaryAsync(
            int idTenant,
            DateOnly? from,
            DateOnly? to,
            DateOnly today,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// As contas a pagar: o gasto do carro e a despesa da loja, numa lista só, do vencimento
        /// mais antigo para o mais novo.
        /// </summary>
        /// <param name="idTenant">Revenda.</param>
        /// <param name="settled">Nulo traz as duas; falso só o que falta pagar; verdadeiro só o pago.</param>
        /// <param name="from">Primeiro dia do vencimento, inclusive. Nulo para sem limite.</param>
        /// <param name="to">Último dia do vencimento, inclusive. Nulo para sem limite.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>As linhas.</returns>
        Task<IReadOnlyList<CashflowLine>> ListPayablesAsync(
            int idTenant,
            bool? settled,
            DateOnly? from,
            DateOnly? to,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// As contas a receber: cada venda cujo esperado em dinheiro ainda tem saldo, do prazo
        /// mais antigo para o mais novo. Venda quitada fica de fora.
        /// </summary>
        /// <param name="idTenant">Revenda.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>As linhas.</returns>
        Task<IReadOnlyList<CashflowLine>> ListReceivablesAsync(
            int idTenant,
            CancellationToken cancellationToken = default);
    }

    /// <summary>Os números do topo do caixa.</summary>
    /// <param name="PayableOpen">O que a revenda ainda deve, somado.</param>
    /// <param name="PayableOverdue">Quanto disso já venceu.</param>
    /// <param name="PayableDueSoon">Quanto vence nos próximos sete dias.</param>
    /// <param name="ReceivableOpen">O que ainda falta receber das vendas.</param>
    /// <param name="ReceivableOverdue">Quanto disso já passou do prazo.</param>
    /// <param name="PaidInPeriod">Quanto saiu no período.</param>
    /// <param name="ReceivedInPeriod">Quanto entrou no período.</param>
    public sealed record CashflowSummary(
        decimal PayableOpen,
        decimal PayableOverdue,
        decimal PayableDueSoon,
        decimal ReceivableOpen,
        decimal ReceivableOverdue,
        decimal PaidInPeriod,
        decimal ReceivedInPeriod);

    /// <summary>
    /// Uma linha do extrato do caixa, venha ela de onde vier.
    /// </summary>
    /// <param name="Code">Identificador público da origem: o gasto, a despesa ou a venda.</param>
    /// <param name="Kind">De onde a linha veio.</param>
    /// <param name="Description">O que é.</param>
    /// <param name="Party">A quem se paga, ou quem paga. Nulo quando ninguém está cadastrado.</param>
    /// <param name="Category">O tipo de gasto, ou a forma de pagamento da venda.</param>
    /// <param name="Amount">Quanto. No que se recebe, é o que ainda falta.</param>
    /// <param name="DueDate">Quando vence. Nulo só quando a venda ficou sem prazo.</param>
    /// <param name="SettledDate">Quando o dinheiro se moveu. Nulo enquanto está em aberto.</param>
    /// <param name="IsSettled">Se já foi pago ou recebido por inteiro.</param>
    /// <param name="VehicleCode">O carro, quando a linha pertence a um.</param>
    /// <param name="Plate">A placa, para a tela mostrar sem outra consulta.</param>
    public sealed record CashflowLine(
        Guid Code,
        CashflowKind Kind,
        string Description,
        string? Party,
        string? Category,
        decimal Amount,
        DateOnly? DueDate,
        DateOnly? SettledDate,
        bool IsSettled,
        Guid? VehicleCode,
        string? Plate);
}
