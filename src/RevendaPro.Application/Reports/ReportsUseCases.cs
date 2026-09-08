using MediatR;
using RevendaPro.Application.Company.DTOs;
using RevendaPro.Domain.Enums;

namespace RevendaPro.Application.Reports.Queries
{
    /// <summary>
    /// A ficha do carro para venda: o que o comprador vê, e nada do que é da casa (M19).
    /// </summary>
    /// <param name="VehicleCode">Identificador público do carro.</param>
    public sealed record GetSaleSheetQuery(Guid VehicleCode) : IRequest<DTOs.SaleSheetDto>;

    /// <summary>
    /// A proposta registrada, em papel timbrado, para o cliente assinar ou guardar (M19).
    /// </summary>
    /// <param name="VehicleCode">Identificador público do carro.</param>
    /// <param name="ProposalCode">Identificador público da proposta.</param>
    public sealed record GetProposalDocumentQuery(Guid VehicleCode, Guid ProposalCode)
        : IRequest<DTOs.ProposalDocumentDto>;
}

namespace RevendaPro.Application.Reports.DTOs
{
    /// <summary>
    /// O carro como o comprador o lê: identificação, o que ele tem, a tabela e o preço.
    ///
    /// O que <b>jamais</b> entra aqui: custo, compra, lucro, pátio, fornecedor, avaria. É um
    /// documento para o comprador, e o que a revenda pagou no carro não é assunto dele.
    /// </summary>
    /// <param name="Company">A revenda, para o cabeçalho e o contato.</param>
    /// <param name="Plate">A placa.</param>
    /// <param name="Brand">A marca.</param>
    /// <param name="Model">O modelo.</param>
    /// <param name="Version">A versão, quando cadastrada.</param>
    /// <param name="ModelYear">Ano do modelo.</param>
    /// <param name="ManufactureYear">Ano de fabricação.</param>
    /// <param name="Color">A cor.</param>
    /// <param name="Mileage">Quilometragem.</param>
    /// <param name="FuelType">Combustível.</param>
    /// <param name="Transmission">Câmbio.</param>
    /// <param name="AdvertisedPrice">O preço anunciado. Nulo sai como "consulte".</param>
    /// <param name="FipeValue">O valor da tabela de referência, quando consultado.</param>
    /// <param name="FipeReferenceDate">O mês da tabela.</param>
    /// <param name="Photos">As fotos, a capa primeiro, no tamanho de card. Até sete.</param>
    /// <param name="IssuedOn">A data do documento.</param>
    public sealed record SaleSheetDto(
        CompanyDto Company,
        string Plate,
        string Brand,
        string Model,
        string? Version,
        short ModelYear,
        short ManufactureYear,
        string? Color,
        int Mileage,
        FuelType FuelType,
        TransmissionType Transmission,
        decimal? AdvertisedPrice,
        decimal? FipeValue,
        DateOnly? FipeReferenceDate,
        IReadOnlyList<byte[]> Photos,
        DateOnly IssuedOn);

    /// <summary>
    /// A proposta como vai para o cliente: a revenda em cima, o cliente, o carro, o valor, a
    /// forma de pagamento, a data, a validade e as condições. Nada do que a proposta calcula
    /// para a casa — sobra, margem, repasse — sai daqui.
    /// </summary>
    /// <param name="Company">A revenda.</param>
    /// <param name="ProposalCode">O código da proposta, impresso como referência.</param>
    /// <param name="ProspectName">Quem recebe a proposta.</param>
    /// <param name="ProspectPhone">O telefone dele, só dígitos, quando informado.</param>
    /// <param name="VehicleName">Marca, modelo e versão, prontos para imprimir.</param>
    /// <param name="Plate">A placa.</param>
    /// <param name="ModelYear">Ano do modelo.</param>
    /// <param name="ManufactureYear">Ano de fabricação.</param>
    /// <param name="Mileage">Quilometragem.</param>
    /// <param name="Color">A cor.</param>
    /// <param name="Amount">O valor proposto.</param>
    /// <param name="PaymentMethod">Como o cliente paga.</param>
    /// <param name="Date">A data da proposta.</param>
    /// <param name="ValidUntil">Até quando a proposta vale.</param>
    /// <param name="Notes">As condições em texto livre, quando escritas.</param>
    /// <param name="CoverPhoto">A foto de capa, quando existe.</param>
    public sealed record ProposalDocumentDto(
        CompanyDto Company,
        Guid ProposalCode,
        string ProspectName,
        string? ProspectPhone,
        string VehicleName,
        string Plate,
        short ModelYear,
        short ManufactureYear,
        int Mileage,
        string? Color,
        decimal Amount,
        PaymentMethod PaymentMethod,
        DateOnly Date,
        DateOnly ValidUntil,
        string? Notes,
        byte[]? CoverPhoto);
}
