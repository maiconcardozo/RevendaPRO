using MediatR;
using RevendaPro.Domain.Enums;

namespace RevendaPro.Application.Yards.Queries
{
    /// <summary>Os pátios da revenda, na ordem em que ela os mostra.</summary>
    public sealed record ListYardsQuery : IRequest<IReadOnlyList<DTOs.YardDto>>;
}

namespace RevendaPro.Application.Yards.Commands
{
    /// <summary>
    /// Cadastra ou edita um pátio.
    /// </summary>
    /// <param name="Code">Nulo cadastra; preenchido edita.</param>
    /// <param name="Name">Como a revenda chama o lugar.</param>
    /// <param name="Kind">Próprio ou de outra pessoa.</param>
    /// <param name="ContactName">Quem responde pelo lugar.</param>
    /// <param name="ContactPhone">Telefone do responsável.</param>
    /// <param name="CutPercent">Repasse combinado em percentual.</param>
    /// <param name="CutAmount">Repasse combinado em valor.</param>
    /// <param name="Notes">Anotação livre.</param>
    /// <param name="Position">Ordem na lista.</param>
    public sealed record SaveYardCommand(
        Guid? Code,
        string Name,
        YardKind Kind,
        string? ContactName,
        string? ContactPhone,
        decimal? CutPercent,
        decimal? CutAmount,
        string? Notes,
        int Position) : IRequest<DTOs.YardDto>;

    /// <summary>Exclui um pátio, logicamente.</summary>
    /// <param name="Code">Identificador público.</param>
    public sealed record DeleteYardCommand(Guid Code) : IRequest;
}

namespace RevendaPro.Application.Yards.DTOs
{
    /// <summary>
    /// Um pátio, como a tela lê.
    /// </summary>
    /// <param name="Code">Identificador público.</param>
    /// <param name="Name">Como a revenda chama o lugar.</param>
    /// <param name="Kind">Próprio ou de outra pessoa.</param>
    /// <param name="ContactName">Quem responde pelo lugar.</param>
    /// <param name="ContactPhone">Telefone do responsável.</param>
    /// <param name="CutPercent">Repasse combinado em percentual.</param>
    /// <param name="CutAmount">Repasse combinado em valor.</param>
    /// <param name="Notes">Anotação livre.</param>
    /// <param name="Position">Ordem na lista.</param>
    /// <param name="VehicleCount">
    /// Quantos carros estão nele agora. É o número que a tela usa para dizer por que a exclusão
    /// foi recusada, antes de a pessoa tentar.
    /// </param>
    public sealed record YardDto(
        Guid Code,
        string Name,
        YardKind Kind,
        string? ContactName,
        string? ContactPhone,
        decimal? CutPercent,
        decimal? CutAmount,
        string? Notes,
        int Position,
        int VehicleCount);
}
