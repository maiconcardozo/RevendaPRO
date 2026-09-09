using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RevendaPro.Api.Authorization;
using RevendaPro.Api.Contracts;
using RevendaPro.Application.Trash.Commands;
using RevendaPro.Application.Trash.DTOs;
using RevendaPro.Application.Trash.Queries;
using RevendaPro.Domain.Enums;

namespace RevendaPro.Api.Controllers
{
    /// <summary>
    /// A lixeira: o que foi apagado por engano, e a porta de volta (M23).
    ///
    /// Uma porta só, com o tipo dizendo em qual tabela bater — é o mesmo desenho da baixa do
    /// caixa no M22, e é o que permite a quarta e a quinta coisa entrarem na lixeira sem tela
    /// nova nem rota nova.
    ///
    /// Não existe aqui um endpoint para apagar de vez, e a ausência é o desenho: o negócio
    /// pediu para guardar, o arquivo jamais saiu do bucket, e um apagar definitivo desfaria a
    /// recuperação administrativa da RNF-08. Uma tela de lixeira é a tentação óbvia para
    /// "esvaziar"; ela segue sem esse botão.
    ///
    /// A tela exigida continua sendo <c>deleted-documents</c>, o nome que ela tinha quando só
    /// mostrava documento. Trocar a chave faria o sincronizador desativar a tela antiga e criar
    /// outra, e toda revenda que já concedeu a permissão a alguém a perderia sem saber. Ver
    /// <c>docs/plans/m23-lixeira.md</c>.
    /// </summary>
    [ApiController]
    [Route("api/trash")]
    [Authorize]
    [RequireScreen("deleted-documents")]
    public sealed class TrashController(IMediator mediator) : ControllerBase
    {
        /// <summary>
        /// O que foi apagado de um tipo, da exclusão mais recente para a mais antiga.
        /// </summary>
        /// <param name="kind">Veículo, gasto ou documento.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>As linhas da lixeira.</returns>
        [HttpGet]
        [ProducesResponseType(
            typeof(SuccessDetails<IReadOnlyList<DeletedItemDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> List(
            [FromQuery] TrashKind kind,
            CancellationToken cancellationToken)
        {
            var items = await mediator.Send(new ListDeletedItemsQuery(kind), cancellationToken);

            return Ok(new SuccessDetails<IReadOnlyList<DeletedItemDto>>(
                StatusCodes.Status200OK, "OK", "Lixeira carregada.",
                HttpContext.Request.Path, items));
        }

        /// <summary>
        /// Devolve à operação o que tinha sido apagado.
        ///
        /// Devolver um carro devolve a ficha inteira — fotos, gastos, documentos e história —,
        /// porque a consulta de cada um deles passa pelo carro. Duas recusas respondem 422, e as
        /// duas dizem o que fazer em seguida: a placa que outro carro já ocupa, com o nome dele,
        /// e o gasto de um carro que continua na lixeira.
        /// </summary>
        /// <param name="kind">Veículo, gasto ou documento.</param>
        /// <param name="code">Identificador público da coisa.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>No content.</returns>
        [HttpPost("{kind}/{code:guid}/restore")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> Restore(
            TrashKind kind,
            Guid code,
            CancellationToken cancellationToken)
        {
            await mediator.Send(new RestoreDeletedItemCommand(kind, code), cancellationToken);

            return NoContent();
        }
    }
}
