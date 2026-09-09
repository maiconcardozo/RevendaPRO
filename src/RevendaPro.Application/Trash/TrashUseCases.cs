using MediatR;
using RevendaPro.Domain.Enums;

namespace RevendaPro.Application.Trash.DTOs
{
    /// <summary>
    /// Uma coisa que foi apagada, como a lixeira precisa mostrá-la (M23).
    ///
    /// Um formato só para os três tipos, de propósito: quem apagou por engano tem uma pergunta
    /// — "onde está o que eu apaguei?" — e a tela é uma, com uma aba por tipo. Três formatos
    /// dariam três tabelas para manter em dia, e a mesma coluna "excluído em" escrita três
    /// vezes.
    ///
    /// O preço é que campo de um tipo vem nulo no outro, e é por isso que cada um diz aqui
    /// quando se aplica.
    /// </summary>
    /// <param name="Kind">Que tipo de coisa é.</param>
    /// <param name="Code">Identificador público dela, que é por onde a volta pede.</param>
    /// <param name="Title">Como se reconhece: a placa, a descrição do gasto, o nome do arquivo.</param>
    /// <param name="Subtitle">
    /// O que completa o título: o carro por extenso, o tipo do gasto. Nulo quando não há —
    /// um gasto cujo tipo saiu do catálogo, por exemplo.
    /// </param>
    /// <param name="Amount">
    /// Quanto: o preço de compra do carro, o valor do gasto. Nulo no documento, que não tem
    /// dinheiro nenhum.
    /// </param>
    /// <param name="Date">A data própria da coisa: quando o gasto foi lançado. Nula no resto.</param>
    /// <param name="DeletedAt">Quando ela foi apagada. É por aqui que a lista se ordena.</param>
    /// <param name="DeletedBy">
    /// Quem apagou, pelo nome — inclusive quem já saiu da revenda. Nulo quando o nome se
    /// perdeu.
    /// </param>
    /// <param name="VehicleCode">
    /// O carro de que a coisa pende, para a tela abrir a ficha. Nulo no próprio carro: ele
    /// está na lixeira, e a ficha dele ainda não abre.
    /// </param>
    /// <param name="VehiclePlate">A placa desse carro.</param>
    /// <param name="VehicleName">Marca e modelo desse carro.</param>
    /// <param name="VehicleIsInYard">
    /// Se esse carro está no pátio. Nulo no próprio carro, onde a pergunta não faz sentido.
    /// É o que diz à pessoa que o gasto só volta depois do carro.
    /// </param>
    /// <param name="DocumentKind">Que espécie de documento é. Nulo no resto.</param>
    /// <param name="SizeInBytes">O tamanho do arquivo. Nulo no resto.</param>
    /// <param name="FileUrl">
    /// Endereço assinado do arquivo, válido por minutos, para conferir antes de devolver. Nulo
    /// no resto.
    /// </param>
    public sealed record DeletedItemDto(
        TrashKind Kind,
        Guid Code,
        string Title,
        string? Subtitle,
        decimal? Amount,
        DateOnly? Date,
        DateTime? DeletedAt,
        string? DeletedBy,
        Guid? VehicleCode,
        string? VehiclePlate,
        string? VehicleName,
        bool? VehicleIsInYard,
        VehicleDocumentKind? DocumentKind,
        int? SizeInBytes,
        string? FileUrl);
}

namespace RevendaPro.Application.Trash.Queries
{
    using RevendaPro.Application.Trash.DTOs;

    /// <summary>
    /// O que foi apagado de um tipo, da exclusão mais recente para a mais antiga (M23).
    ///
    /// Administrativa de propósito: a lixeira mostra exatamente o que toda outra leitura do
    /// sistema esconde, e por isso vive atrás de uma tela própria, que pela ADR-0002 é uma
    /// permissão própria. A chave dessa tela continua sendo <c>deleted-documents</c>, e o
    /// motivo está em <c>docs/plans/m23-lixeira.md</c>: trocá-la faria toda revenda perder a
    /// permissão que já concedeu.
    /// </summary>
    /// <param name="Kind">Em qual tabela bater.</param>
    public sealed record ListDeletedItemsQuery(TrashKind Kind)
        : IRequest<IReadOnlyList<DeletedItemDto>>;
}

namespace RevendaPro.Application.Trash.Commands
{
    using RevendaPro.Domain.Enums;

    /// <summary>
    /// Devolve à operação uma coisa que tinha sido apagada (M23).
    ///
    /// Devolver um carro devolve a <b>ficha inteira</b>, sem tocar em linha nenhuma dela: as
    /// fotos, os gastos e os documentos sumiram porque a consulta de cada um passa pelo carro,
    /// e voltam pelo mesmo motivo. O que tinha sido apagado antes, um a um, continua apagado —
    /// percorrer os filhos para "reativar tudo" ressuscitaria a foto que alguém apagou de
    /// propósito na semana passada.
    ///
    /// Comando para apagar de vez, jamais: guardar foi o que o negócio pediu, e a ausência é o
    /// desenho desde o M9.
    /// </summary>
    /// <param name="Kind">Em qual tabela bater.</param>
    /// <param name="Code">Identificador público da coisa.</param>
    public sealed record RestoreDeletedItemCommand(TrashKind Kind, Guid Code) : IRequest;
}
