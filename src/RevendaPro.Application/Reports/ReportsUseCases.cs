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
}
