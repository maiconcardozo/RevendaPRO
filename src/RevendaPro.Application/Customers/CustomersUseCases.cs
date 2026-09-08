using MediatR;
using RevendaPro.Domain.Enums;

namespace RevendaPro.Application.Customers.Queries
{
    /// <summary>
    /// Os clientes da revenda, por nome, filtrados por um trecho do nome, do telefone ou do
    /// documento. É a lista da tela Clientes e a busca do seletor da proposta.
    /// </summary>
    /// <param name="Search">Trecho a procurar. Nulo ou vazio traz todos.</param>
    public sealed record ListCustomersQuery(string? Search) : IRequest<IReadOnlyList<DTOs.CustomerDto>>;

    /// <summary>A ficha de um cliente: os dados e o histórico de propostas e compras.</summary>
    /// <param name="Code">Identificador público.</param>
    public sealed record GetCustomerQuery(Guid Code) : IRequest<DTOs.CustomerDetailDto>;
}

namespace RevendaPro.Application.Customers.Commands
{
    /// <summary>
    /// Cadastra ou edita um cliente.
    /// </summary>
    /// <param name="Code">Nulo cadastra; preenchido edita.</param>
    /// <param name="Name">Como a pessoa se apresenta.</param>
    /// <param name="Document">CPF ou CNPJ.</param>
    /// <param name="Phone">Telefone.</param>
    /// <param name="Email">E-mail.</param>
    /// <param name="Address">Endereço, em uma linha.</param>
    /// <param name="Notes">Anotação livre.</param>
    /// <param name="ConfirmSamePhone">
    /// Telefone igual ao de outro cliente é aviso, e não recusa: o telefone da loja parceira
    /// pode estar em mais de um comprador. Verdadeiro diz "é outra pessoa, siga".
    /// </param>
    public sealed record SaveCustomerCommand(
        Guid? Code,
        string Name,
        string? Document,
        string? Phone,
        string? Email,
        string? Address,
        string? Notes,
        bool ConfirmSamePhone = false) : IRequest<DTOs.CustomerDto>;

    /// <summary>Exclui um cliente, logicamente. Recusado quando ele tem história.</summary>
    /// <param name="Code">Identificador público.</param>
    public sealed record DeleteCustomerCommand(Guid Code) : IRequest;
}

namespace RevendaPro.Application.Customers.DTOs
{
    /// <summary>
    /// Um cliente, como a tela lê, com o resumo do que ele já fez com a revenda.
    /// </summary>
    /// <param name="Code">Identificador público.</param>
    /// <param name="Name">Como a pessoa se apresenta.</param>
    /// <param name="Document">CPF ou CNPJ, só dígitos.</param>
    /// <param name="Phone">Telefone, só dígitos. É por onde o WhatsApp chega.</param>
    /// <param name="Email">E-mail.</param>
    /// <param name="Address">Endereço, em uma linha.</param>
    /// <param name="Notes">Anotação livre.</param>
    /// <param name="ProposalCount">Quantas propostas fez, de toda situação.</param>
    /// <param name="SaleCount">Quantos carros comprou.</param>
    /// <param name="BoughtTotal">Quanto pagou pelos carros, somado.</param>
    /// <param name="LastDate">A data da proposta ou compra mais recente.</param>
    public sealed record CustomerDto(
        Guid Code,
        string Name,
        string? Document,
        string? Phone,
        string? Email,
        string? Address,
        string? Notes,
        int ProposalCount,
        int SaleCount,
        decimal BoughtTotal,
        DateOnly? LastDate);

    /// <summary>A ficha do cliente: os dados e a história.</summary>
    /// <param name="Customer">O cliente.</param>
    /// <param name="Proposals">As propostas, da mais recente para a mais antiga.</param>
    /// <param name="Sales">As compras, da mais recente para a mais antiga.</param>
    public sealed record CustomerDetailDto(
        CustomerDto Customer,
        IReadOnlyList<CustomerProposalDto> Proposals,
        IReadOnlyList<CustomerSaleDto> Sales);

    /// <summary>Uma proposta na ficha do cliente, com o carro.</summary>
    /// <param name="Code">Código da proposta.</param>
    /// <param name="Date">Quando.</param>
    /// <param name="Amount">Quanto ofereceu.</param>
    /// <param name="Status">A situação.</param>
    /// <param name="PaymentMethod">Como pagaria.</param>
    /// <param name="VehicleCode">Código do carro.</param>
    /// <param name="Plate">Placa.</param>
    /// <param name="VehicleName">Marca, modelo e versão, como a tela mostra.</param>
    /// <param name="ModelYear">Ano do modelo.</param>
    public sealed record CustomerProposalDto(
        Guid Code,
        DateOnly Date,
        decimal Amount,
        ProposalStatus Status,
        PaymentMethod PaymentMethod,
        Guid VehicleCode,
        string Plate,
        string VehicleName,
        int ModelYear);

    /// <summary>Uma compra na ficha do cliente, com o carro.</summary>
    /// <param name="Code">Código da venda.</param>
    /// <param name="Date">Quando.</param>
    /// <param name="Amount">Por quanto saiu.</param>
    /// <param name="PaymentMethod">Como pagou.</param>
    /// <param name="HadTradeIn">Se entrou carro na troca.</param>
    /// <param name="VehicleCode">Código do carro.</param>
    /// <param name="Plate">Placa.</param>
    /// <param name="VehicleName">Marca, modelo e versão, como a tela mostra.</param>
    /// <param name="ModelYear">Ano do modelo.</param>
    public sealed record CustomerSaleDto(
        Guid Code,
        DateOnly Date,
        decimal Amount,
        PaymentMethod PaymentMethod,
        bool HadTradeIn,
        Guid VehicleCode,
        string Plate,
        string VehicleName,
        int ModelYear);
}
