using MediatR;

namespace RevendaPro.Application.Suppliers.Queries
{
    /// <summary>Os fornecedores da revenda, por nome, com quantos gastos apontam para cada um.</summary>
    public sealed record ListSuppliersQuery : IRequest<IReadOnlyList<DTOs.SupplierDto>>;

    /// <summary>Os ramos de fornecedor da revenda, na ordem em que ela os mostra.</summary>
    public sealed record ListSupplierSegmentsQuery : IRequest<IReadOnlyList<DTOs.SupplierSegmentDto>>;
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
}
