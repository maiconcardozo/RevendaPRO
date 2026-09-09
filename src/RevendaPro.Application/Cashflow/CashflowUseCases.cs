using MediatR;
using RevendaPro.Domain.Enums;

namespace RevendaPro.Application.Cashflow.Queries
{
    /// <summary>
    /// O caixa de um período (M22): os totais, o que falta pagar, o que falta receber, e o que
    /// já se moveu. Sem período, o mês corrente é escolhido pela tela.
    /// </summary>
    /// <param name="From">Primeiro dia, inclusive. Nulo para sem limite.</param>
    /// <param name="To">Último dia, inclusive. Nulo para sem limite.</param>
    public sealed record GetCashflowQuery(DateOnly? From, DateOnly? To) : IRequest<DTOs.CashflowDto>;
}

namespace RevendaPro.Application.Cashflow.Commands
{
    /// <summary>
    /// Dá baixa numa conta a pagar, venha ela do carro ou da loja (M22).
    ///
    /// Uma porta só para os dois lados: quem está no caixa vê uma lista, e a origem da linha é
    /// problema do código.
    /// </summary>
    /// <param name="Kind">De onde a linha veio.</param>
    /// <param name="Code">Identificador público do gasto ou da despesa.</param>
    /// <param name="IsPaid">Verdadeiro paga; falso devolve para previsto.</param>
    /// <param name="PaidDate">O dia em que o dinheiro saiu. Sem data, é hoje.</param>
    public sealed record SettlePayableCommand(
        CashflowKind Kind,
        Guid Code,
        bool IsPaid = true,
        DateOnly? PaidDate = null) : IRequest;
}

namespace RevendaPro.Application.Cashflow.DTOs
{
    /// <summary>O caixa como a tela lê.</summary>
    /// <param name="From">Primeiro dia do período.</param>
    /// <param name="To">Último dia do período.</param>
    /// <param name="PayableOpen">O que a revenda ainda deve.</param>
    /// <param name="PayableOverdue">Quanto disso já venceu.</param>
    /// <param name="PayableDueSoon">Quanto vence nos próximos sete dias.</param>
    /// <param name="ReceivableOpen">O que ainda falta receber das vendas.</param>
    /// <param name="ReceivableOverdue">Quanto disso já passou do prazo.</param>
    /// <param name="PaidInPeriod">Quanto saiu no período.</param>
    /// <param name="ReceivedInPeriod">Quanto entrou no período.</param>
    /// <param name="Payables">As contas a pagar em aberto, do vencimento mais antigo.</param>
    /// <param name="Receivables">As vendas com saldo, do prazo mais antigo.</param>
    public sealed record CashflowDto(
        DateOnly? From,
        DateOnly? To,
        decimal PayableOpen,
        decimal PayableOverdue,
        decimal PayableDueSoon,
        decimal ReceivableOpen,
        decimal ReceivableOverdue,
        decimal PaidInPeriod,
        decimal ReceivedInPeriod,
        IReadOnlyList<CashflowLineDto> Payables,
        IReadOnlyList<CashflowLineDto> Receivables);

    /// <summary>Uma linha do extrato do caixa.</summary>
    /// <param name="Code">Identificador público da origem.</param>
    /// <param name="Kind">De onde a linha veio: 1 gasto do carro, 2 despesa da loja, 3 venda a receber.</param>
    /// <param name="Description">O que é.</param>
    /// <param name="Party">A quem se paga, ou quem paga.</param>
    /// <param name="Category">O tipo de gasto, ou a forma de pagamento da venda.</param>
    /// <param name="Amount">Quanto. No que se recebe, é o que ainda falta.</param>
    /// <param name="DueDate">Quando vence.</param>
    /// <param name="SettledDate">Quando o dinheiro se moveu.</param>
    /// <param name="IsSettled">Se já foi pago.</param>
    /// <param name="IsOverdue">Vencida e ainda em aberto, hoje.</param>
    /// <param name="VehicleCode">O carro, quando a linha pertence a um.</param>
    /// <param name="Plate">A placa.</param>
    public sealed record CashflowLineDto(
        Guid Code,
        CashflowKind Kind,
        string Description,
        string? Party,
        string? Category,
        decimal Amount,
        DateOnly? DueDate,
        DateOnly? SettledDate,
        bool IsSettled,
        bool IsOverdue,
        Guid? VehicleCode,
        string? Plate);
}
