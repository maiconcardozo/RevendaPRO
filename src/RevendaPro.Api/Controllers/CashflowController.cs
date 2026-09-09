using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RevendaPro.Api.Authorization;
using RevendaPro.Api.Contracts;
using RevendaPro.Application.Cashflow.Commands;
using RevendaPro.Application.Cashflow.DTOs;
using RevendaPro.Application.Cashflow.Queries;

namespace RevendaPro.Api.Controllers
{
    /// <summary>
    /// O dinheiro no tempo (M22): o que vence, o que entrou, o que atrasou.
    ///
    /// Junta as três origens do caixa — o gasto do carro, a despesa da loja e o que falta
    /// receber de cada venda — numa tela só. Ver <c>docs/plans/m22-caixa.md</c>.
    /// </summary>
    [ApiController]
    [Route("api/cashflow")]
    [Authorize]
    [RequireScreen("cashflow")]
    public sealed class CashflowController(IMediator mediator) : ControllerBase
    {
        /// <summary>
        /// O caixa de um período. O que se deve e o que se tem a receber vêm inteiros; o período
        /// delimita só o que já se moveu.
        /// </summary>
        /// <param name="from">Primeiro dia, inclusive.</param>
        /// <param name="to">Último dia, inclusive.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>O caixa.</returns>
        [HttpGet]
        [ProducesResponseType(typeof(SuccessDetails<CashflowDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Get(
            [FromQuery] DateOnly? from,
            [FromQuery] DateOnly? to,
            CancellationToken cancellationToken)
        {
            var cashflow = await mediator.Send(new GetCashflowQuery(from, to), cancellationToken);

            return Ok(new SuccessDetails<CashflowDto>(
                StatusCodes.Status200OK, "OK", "Caixa carregado.",
                HttpContext.Request.Path, cashflow));
        }

        /// <summary>
        /// Dá baixa numa conta a pagar, venha ela do carro ou da loja. Sem corpo, paga hoje.
        /// </summary>
        /// <param name="command">De onde a linha veio, se pagou, e quando.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>No content.</returns>
        [HttpPatch("payables")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> Settle(
            [FromBody] SettlePayableCommand command,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);

            await mediator.Send(command, cancellationToken);

            return NoContent();
        }
    }
}
