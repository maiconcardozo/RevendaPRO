using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RevendaPro.Api.Authorization;
using RevendaPro.Api.Contracts;
using RevendaPro.Application.Yards.Commands;
using RevendaPro.Application.Yards.DTOs;
using RevendaPro.Application.Yards.Queries;

namespace RevendaPro.Api.Controllers
{
    /// <summary>
    /// Os lugares onde os carros ficam: o pátio da revenda, e as lojas de terceiros onde ela
    /// deixou carro para vender.
    ///
    /// Um cadastro só, com um tipo dentro. Guardado pela tela própria, porque tela é permissão
    /// (ADR-0002). Ver <c>docs/plans/m14-patios.md</c>.
    /// </summary>
    [ApiController]
    [Route("api/yards")]
    [Authorize]
    [RequireScreen("yards")]
    public sealed class YardsController(IMediator mediator) : ControllerBase
    {
        /// <summary>Os pátios da revenda, com quantos carros estão em cada um.</summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>Os pátios.</returns>
        [HttpGet]
        [ProducesResponseType(typeof(SuccessDetails<IReadOnlyList<YardDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> List(CancellationToken cancellationToken)
        {
            var yards = await mediator.Send(new ListYardsQuery(), cancellationToken);

            return Ok(new SuccessDetails<IReadOnlyList<YardDto>>(
                StatusCodes.Status200OK, "OK", "Pátios carregados.",
                HttpContext.Request.Path, yards));
        }

        /// <summary>Cadastra um pátio.</summary>
        /// <param name="command">Os dados do pátio.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>O pátio cadastrado.</returns>
        [HttpPost]
        [ProducesResponseType(typeof(SuccessDetails<YardDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> Create(
            [FromBody] SaveYardCommand command,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);

            var yard = await mediator.Send(command with { Code = null }, cancellationToken);

            return Ok(new SuccessDetails<YardDto>(
                StatusCodes.Status200OK, "OK", "Pátio cadastrado.",
                HttpContext.Request.Path, yard));
        }

        /// <summary>Edita um pátio.</summary>
        /// <param name="code">Identificador público.</param>
        /// <param name="command">Os dados do pátio.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>O pátio editado.</returns>
        [HttpPut("{code:guid}")]
        [ProducesResponseType(typeof(SuccessDetails<YardDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> Update(
            Guid code,
            [FromBody] SaveYardCommand command,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);

            var yard = await mediator.Send(command with { Code = code }, cancellationToken);

            return Ok(new SuccessDetails<YardDto>(
                StatusCodes.Status200OK, "OK", "Pátio atualizado.",
                HttpContext.Request.Path, yard));
        }

        /// <summary>Exclui um pátio, logicamente.</summary>
        /// <param name="code">Identificador público.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>No content.</returns>
        [HttpDelete("{code:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> Delete(Guid code, CancellationToken cancellationToken)
        {
            await mediator.Send(new DeleteYardCommand(code), cancellationToken);

            return NoContent();
        }
    }
}
