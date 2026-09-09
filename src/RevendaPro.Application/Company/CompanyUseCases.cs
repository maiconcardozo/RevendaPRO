using MediatR;
using RevendaPro.Domain.Interfaces.Security;

namespace RevendaPro.Application.Company.Queries
{
    /// <summary>Os dados da revenda de quem está logado: o que os documentos imprimem.</summary>
    public sealed record GetCompanyQuery : IRequest<DTOs.CompanyDto>;

    /// <summary>
    /// O logotipo da revenda de quem está logado, ou nulo enquanto ela não subiu um (M25).
    ///
    /// Servido pela própria API, como a foto do usuário: a tela e o PDF são os dois consumidores,
    /// e nenhum ganha com um endereço assinado.
    /// </summary>
    public sealed record ReadCompanyLogoQuery : IRequest<StoredPhoto?>;
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

    /// <summary>
    /// Troca o logotipo da revenda (M25). O que chega já veio recortado pelo navegador, na
    /// proporção do papel; o anterior é apagado do bucket, porque identidade visual tem versão
    /// nenhuma a guardar.
    /// </summary>
    /// <param name="Content">Os bytes da imagem.</param>
    public sealed record SaveCompanyLogoCommand(Stream Content) : IRequest<DTOs.CompanyDto>;

    /// <summary>Remove o logotipo da revenda (M25). Os papéis voltam a sair como no M19.</summary>
    public sealed record RemoveCompanyLogoCommand : IRequest<DTOs.CompanyDto>;
}

namespace RevendaPro.Application.Company.DTOs
{
    /// <summary>A revenda como a tela e os documentos leem.</summary>
    /// <param name="Name">Como a revenda se chama.</param>
    /// <param name="Document">CNPJ ou CPF, só dígitos.</param>
    /// <param name="Phone">Telefone, só dígitos.</param>
    /// <param name="Email">E-mail.</param>
    /// <param name="Address">Endereço, em uma linha.</param>
    /// <param name="HasLogo">Se a revenda tem logotipo para o papel e para a tela (M25).</param>
    /// <param name="LogoVersion">
    /// Muda a cada troca do logotipo, para a tela pedir a imagem de novo em vez de mostrar a que
    /// o navegador guardou. Nulo sem logotipo.
    /// </param>
    public sealed record CompanyDto(
        string Name,
        string? Document,
        string? Phone,
        string? Email,
        string? Address,
        bool HasLogo,
        string? LogoVersion);
}
