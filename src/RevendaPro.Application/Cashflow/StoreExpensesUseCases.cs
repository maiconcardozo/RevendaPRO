using MediatR;

namespace RevendaPro.Application.Cashflow.Queries
{
    /// <summary>
    /// As despesas da loja num período (M22). Sem período é tudo, que é como a tela abre no
    /// primeiro uso; a tela manda o mês.
    /// </summary>
    /// <param name="From">Primeiro dia, inclusive. Nulo para sem limite.</param>
    /// <param name="To">Último dia, inclusive. Nulo para sem limite.</param>
    public sealed record ListStoreExpensesQuery(DateOnly? From, DateOnly? To)
        : IRequest<IReadOnlyList<DTOs.StoreExpenseDto>>;
}

namespace RevendaPro.Application.Cashflow.Commands
{
    /// <summary>
    /// Lança ou edita uma despesa da loja (M22).
    /// </summary>
    /// <param name="Code">Nulo lança; preenchido edita.</param>
    /// <param name="Description">O que é: "Aluguel de outubro".</param>
    /// <param name="ExpenseTypeCode">O tipo, do catálogo da revenda.</param>
    /// <param name="SupplierCode">A quem se paga. Nulo para imposto e taxa.</param>
    /// <param name="Amount">Quanto.</param>
    /// <param name="Date">A que dia a despesa pertence.</param>
    /// <param name="DueDate">Quando vence. Sem prazo, é a data da despesa.</param>
    /// <param name="IsPaid">Se já foi paga.</param>
    /// <param name="PaidDate">Quando o dinheiro saiu.</param>
    /// <param name="Notes">Anotação livre.</param>
    public sealed record SaveStoreExpenseCommand(
        Guid? Code,
        string Description,
        Guid ExpenseTypeCode,
        Guid? SupplierCode,
        decimal Amount,
        DateOnly Date,
        DateOnly? DueDate,
        bool IsPaid,
        DateOnly? PaidDate,
        string? Notes) : IRequest<DTOs.StoreExpenseDto>;

    /// <summary>Dá baixa numa despesa da loja, ou desfaz a baixa (M22).</summary>
    /// <param name="Code">Identificador público.</param>
    /// <param name="IsPaid">Verdadeiro paga; falso devolve a despesa para prevista.</param>
    /// <param name="PaidDate">O dia em que o dinheiro saiu. Sem data, é hoje.</param>
    public sealed record PayStoreExpenseCommand(
        Guid Code,
        bool IsPaid = true,
        DateOnly? PaidDate = null) : IRequest;

    /// <summary>Exclui uma despesa da loja, logicamente.</summary>
    /// <param name="Code">Identificador público.</param>
    public sealed record DeleteStoreExpenseCommand(Guid Code) : IRequest;
}

namespace RevendaPro.Application.Cashflow.DTOs
{
    /// <summary>Uma despesa da loja, como a tela lê.</summary>
    /// <param name="Code">Identificador público.</param>
    /// <param name="Description">O que é.</param>
    /// <param name="ExpenseTypeCode">O tipo.</param>
    /// <param name="ExpenseTypeName">O nome do tipo, para a lista mostrar sem outra consulta.</param>
    /// <param name="SupplierCode">A quem se paga.</param>
    /// <param name="SupplierName">O nome do fornecedor, para a lista.</param>
    /// <param name="Amount">Quanto.</param>
    /// <param name="Date">A que dia a despesa pertence.</param>
    /// <param name="DueDate">Quando vence.</param>
    /// <param name="PaidDate">Quando o dinheiro saiu. Nulo enquanto está previsto.</param>
    /// <param name="IsPaid">Falso é previsto: o dinheiro ainda está na conta.</param>
    /// <param name="IsOverdue">Vencida e sem pagamento, hoje.</param>
    /// <param name="Notes">Anotação livre.</param>
    public sealed record StoreExpenseDto(
        Guid Code,
        string Description,
        Guid ExpenseTypeCode,
        string ExpenseTypeName,
        Guid? SupplierCode,
        string? SupplierName,
        decimal Amount,
        DateOnly Date,
        DateOnly DueDate,
        DateOnly? PaidDate,
        bool IsPaid,
        bool IsOverdue,
        string? Notes);
}
