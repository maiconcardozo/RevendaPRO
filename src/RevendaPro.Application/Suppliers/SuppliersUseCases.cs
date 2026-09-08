using MediatR;

namespace RevendaPro.Application.Suppliers.Queries
{
    /// <summary>Os fornecedores da revenda, por nome, com quantos gastos apontam para cada um.</summary>
    public sealed record ListSuppliersQuery : IRequest<IReadOnlyList<DTOs.SupplierDto>>;

    /// <summary>Os ramos de fornecedor da revenda, na ordem em que ela os mostra.</summary>
    public sealed record ListSupplierSegmentsQuery : IRequest<IReadOnlyList<DTOs.SupplierSegmentDto>>;

    /// <summary>
    /// Quanto foi para cada fornecedor da revenda num período. Sem período é "desde o início",
    /// que é como a tela Fornecedores abre.
    /// </summary>
    /// <param name="From">Primeiro dia, inclusive. Nulo para sem limite.</param>
    /// <param name="To">Último dia, inclusive. Nulo para sem limite.</param>
    public sealed record ListSupplierSpendingQuery(DateOnly? From, DateOnly? To)
        : IRequest<IReadOnlyList<DTOs.SupplierSpendDto>>;

    /// <summary>
    /// O painel do gasto com fornecedores: os totais, o ranking, e as somas por ramo, por tipo
    /// e por mês. Sem período é "desde o início"; a série mensal cobre os últimos doze meses
    /// quando o período fica aberto.
    /// </summary>
    /// <param name="From">Primeiro dia, inclusive. Nulo para sem limite.</param>
    /// <param name="To">Último dia, inclusive. Nulo para sem limite.</param>
    public sealed record GetSupplierStatisticsQuery(DateOnly? From, DateOnly? To)
        : IRequest<DTOs.SupplierStatisticsDto>;

    /// <summary>A ficha de um fornecedor: totais, quebra por tipo e cada gasto com o carro.</summary>
    /// <param name="Code">Identificador público do fornecedor.</param>
    /// <param name="From">Primeiro dia, inclusive. Nulo para sem limite.</param>
    /// <param name="To">Último dia, inclusive. Nulo para sem limite.</param>
    public sealed record GetSupplierStatementQuery(Guid Code, DateOnly? From, DateOnly? To)
        : IRequest<DTOs.SupplierStatementDto>;
}

namespace RevendaPro.Application.Suppliers.Commands
{
    /// <summary>
    /// Cadastra ou edita um fornecedor.
    /// </summary>
    /// <param name="Code">Nulo cadastra; preenchido edita.</param>
    /// <param name="Name">Como a revenda chama o fornecedor.</param>
    /// <param name="SegmentCode">O ramo, pelo código público.</param>
    /// <param name="ContactName">Com quem falar lá.</param>
    /// <param name="ContactPhone">Telefone.</param>
    /// <param name="Document">CNPJ ou CPF.</param>
    /// <param name="Notes">Anotação livre.</param>
    public sealed record SaveSupplierCommand(
        Guid? Code,
        string Name,
        Guid SegmentCode,
        string? ContactName,
        string? ContactPhone,
        string? Document,
        string? Notes) : IRequest<DTOs.SupplierDto>;

    /// <summary>Exclui um fornecedor, logicamente.</summary>
    /// <param name="Code">Identificador público.</param>
    public sealed record DeleteSupplierCommand(Guid Code) : IRequest;

    /// <summary>Cadastra ou edita um ramo de fornecedor.</summary>
    /// <param name="Code">Nulo cadastra; preenchido edita.</param>
    /// <param name="Name">Como a revenda chama o ramo.</param>
    /// <param name="Position">Ordem na lista.</param>
    public sealed record SaveSupplierSegmentCommand(
        Guid? Code,
        string Name,
        int Position) : IRequest<DTOs.SupplierSegmentDto>;

    /// <summary>Exclui um ramo de fornecedor, logicamente.</summary>
    /// <param name="Code">Identificador público.</param>
    public sealed record DeleteSupplierSegmentCommand(Guid Code) : IRequest;
}

namespace RevendaPro.Application.Suppliers.DTOs
{
    /// <summary>
    /// Um fornecedor, como a tela lê.
    /// </summary>
    /// <param name="Code">Identificador público.</param>
    /// <param name="Name">Como a revenda chama o fornecedor.</param>
    /// <param name="SegmentCode">O ramo, pelo código público.</param>
    /// <param name="SegmentName">O nome do ramo, para a lista mostrar sem outra consulta.</param>
    /// <param name="ContactName">Com quem falar lá.</param>
    /// <param name="ContactPhone">Telefone, só dígitos.</param>
    /// <param name="Document">CNPJ ou CPF, só dígitos.</param>
    /// <param name="Notes">Anotação livre.</param>
    /// <param name="ExpenseCount">
    /// Quantos gastos apontam para ele. É o número que a tela usa para dizer por que a exclusão
    /// foi recusada, antes de a pessoa tentar.
    /// </param>
    public sealed record SupplierDto(
        Guid Code,
        string Name,
        Guid SegmentCode,
        string SegmentName,
        string? ContactName,
        string? ContactPhone,
        string? Document,
        string? Notes,
        int ExpenseCount);

    /// <summary>Um ramo de fornecedor, como a tela lê.</summary>
    /// <param name="Code">Identificador público.</param>
    /// <param name="Name">Como a revenda chama o ramo.</param>
    /// <param name="Position">Ordem na lista.</param>
    /// <param name="SupplierCount">Quantos fornecedores estão nele.</param>
    public sealed record SupplierSegmentDto(
        Guid Code,
        string Name,
        int Position,
        int SupplierCount);

    /// <summary>
    /// Quanto foi para um fornecedor num período — a linha do ranking e do card.
    /// </summary>
    /// <param name="Code">Identificador público.</param>
    /// <param name="Name">Como a revenda chama o fornecedor.</param>
    /// <param name="SegmentName">O ramo.</param>
    /// <param name="PaidTotal">O que foi pago. É o "quanto gastei".</param>
    /// <param name="PlannedTotal">O que está previsto, fora do custo real (RF-11).</param>
    /// <param name="ExpenseCount">Quantos gastos, pagos e previstos.</param>
    /// <param name="LastDate">A data do gasto mais recente.</param>
    public sealed record SupplierSpendDto(
        Guid Code,
        string Name,
        string SegmentName,
        decimal PaidTotal,
        decimal PlannedTotal,
        int ExpenseCount,
        DateOnly? LastDate);

    /// <summary>Quanto foi para um fornecedor em um tipo de gasto.</summary>
    /// <param name="ExpenseTypeName">O tipo.</param>
    /// <param name="PaidTotal">O que foi pago nele.</param>
    /// <param name="ExpenseCount">Quantos gastos, pagos e previstos.</param>
    public sealed record SupplierTypeSpendDto(string ExpenseTypeName, decimal PaidTotal, int ExpenseCount);

    /// <summary>Um gasto de um fornecedor, com o carro em que foi feito.</summary>
    /// <param name="Code">Identificador público do gasto.</param>
    /// <param name="Date">Quando.</param>
    /// <param name="Description">O que foi.</param>
    /// <param name="ExpenseTypeName">O tipo.</param>
    /// <param name="Amount">Quanto.</param>
    /// <param name="IsPaid">Pago ou previsto.</param>
    /// <param name="VehicleCode">Identificador público do carro, para a placa levar à ficha.</param>
    /// <param name="Plate">A placa.</param>
    /// <param name="VehicleName">Marca, modelo e ano, prontos para imprimir.</param>
    public sealed record SupplierExpenseDto(
        Guid Code,
        DateOnly Date,
        string Description,
        string ExpenseTypeName,
        decimal Amount,
        bool IsPaid,
        Guid VehicleCode,
        string Plate,
        string VehicleName);

    /// <summary>Uma fatia do gasto com fornecedores: um ramo, um tipo de gasto ou um mês.</summary>
    /// <param name="Key">O código público do ramo ou do tipo; para o mês, "AAAA-MM".</param>
    /// <param name="Name">O nome do ramo ou do tipo; para o mês, o rótulo curto ("set/26").</param>
    /// <param name="PaidTotal">O que foi pago.</param>
    /// <param name="PlannedTotal">O que está previsto.</param>
    /// <param name="ExpenseCount">Quantos gastos.</param>
    public sealed record SpendSliceDto(
        string Key,
        string Name,
        decimal PaidTotal,
        decimal PlannedTotal,
        int ExpenseCount);

    /// <summary>
    /// O painel do gasto com fornecedores num período: o que a tela Fornecedores mostra em cima
    /// e o que o dashboard mostra no bloco por fornecedor.
    /// </summary>
    /// <param name="From">Primeiro dia lido, ou nulo.</param>
    /// <param name="To">Último dia lido, ou nulo.</param>
    /// <param name="PaidTotal">O que foi pago a fornecedores. É o "quanto gastei".</param>
    /// <param name="PlannedTotal">O que está previsto com fornecedores.</param>
    /// <param name="ExpenseCount">Quantos gastos com fornecedor.</param>
    /// <param name="SupplierCount">Quantos fornecedores receberam algo.</param>
    /// <param name="VehicleCount">Em quantos carros.</param>
    /// <param name="UnassignedPaid">O que foi pago sem fornecedor no gasto, para o painel dizer quanto do dinheiro ainda está sem nome.</param>
    /// <param name="BySupplier">O ranking, do maior para o menor.</param>
    /// <param name="BySegment">A soma por ramo, do maior para o menor.</param>
    /// <param name="ByType">A soma por tipo de gasto, do maior para o menor.</param>
    /// <param name="ByMonth">A soma por mês, em ordem cronológica, com os meses vazios preenchidos.</param>
    public sealed record SupplierStatisticsDto(
        DateOnly? From,
        DateOnly? To,
        decimal PaidTotal,
        decimal PlannedTotal,
        int ExpenseCount,
        int SupplierCount,
        int VehicleCount,
        decimal UnassignedPaid,
        IReadOnlyList<SupplierSpendDto> BySupplier,
        IReadOnlyList<SpendSliceDto> BySegment,
        IReadOnlyList<SpendSliceDto> ByType,
        IReadOnlyList<SpendSliceDto> ByMonth);

    /// <summary>
    /// A ficha de um fornecedor: a resposta para "quanto já foi para ele, em que carros, e em quê".
    /// </summary>
    /// <param name="Supplier">O fornecedor.</param>
    /// <param name="PaidTotal">O que foi pago no período.</param>
    /// <param name="PlannedTotal">O que está previsto no período.</param>
    /// <param name="ByType">A quebra por tipo de gasto, do maior para o menor.</param>
    /// <param name="Expenses">Cada gasto, do mais recente para o mais antigo.</param>
    public sealed record SupplierStatementDto(
        SupplierDto Supplier,
        decimal PaidTotal,
        decimal PlannedTotal,
        IReadOnlyList<SupplierTypeSpendDto> ByType,
        IReadOnlyList<SupplierExpenseDto> Expenses);
}
