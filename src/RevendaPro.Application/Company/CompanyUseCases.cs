using MediatR;

namespace RevendaPro.Application.Company.Queries
{
    /// <summary>Os dados da revenda de quem está logado: o que os documentos imprimem.</summary>
    public sealed record GetCompanyQuery : IRequest<DTOs.CompanyDto>;
}

namespace RevendaPro.Application.Company.Commands
{
    /// <summary>Edita os dados da revenda.</summary>
    /// <param name="Name">Como a revenda se chama, no papel e na tela.</param>
    /// <param name="Document">CNPJ ou CPF.</param>
    /// <param name="Phone">Telefone.</param>
    /// <param name="Email">E-mail.</param>
    /// <param name="Address">Endereço, em uma linha.</param>
    public sealed record SaveCompanyCommand(
        string Name,
        string? Document,
        string? Phone,
        string? Email,
        string? Address) : IRequest<DTOs.CompanyDto>;
}

namespace RevendaPro.Application.Company.DTOs
{
    /// <summary>A revenda como a tela e os documentos leem.</summary>
    /// <param name="Name">Como a revenda se chama.</param>
    /// <param name="Document">CNPJ ou CPF, só dígitos.</param>
    /// <param name="Phone">Telefone, só dígitos.</param>
    /// <param name="Email">E-mail.</param>
    /// <param name="Address">Endereço, em uma linha.</param>
    public sealed record CompanyDto(
        string Name,
        string? Document,
        string? Phone,
        string? Email,
        string? Address);
}
