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
    /// O que a loja paga e que jamais pertence a um carro: aluguel, energia, salário, imposto
    /// (M22).
    ///
    /// Guardado inteiro pela tela do caixa: quem lança o aluguel é quem olha o que a loja deve.
    /// Ver <c>docs/plans/m22-caixa.md</c>.
    /// </summary>
    [ApiController]
    [Route("api/store-expenses")]
    [Authorize]
    [RequireScreen("cashflow")]
    public sealed class StoreExpensesController(IMediator mediator) : ControllerBase
    {
        /// <summary>As despesas da loja num período. Sem período, todas.</summary>
        /// <param name="from">Primeiro dia, inclusive.</param>
        /// <param name="to">Último dia, inclusive.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>As despesas.</returns>
        [HttpGet]
        [ProducesResponseType(typeof(SuccessDetails<IReadOnlyList<StoreExpenseDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> List(
            [FromQuery] DateOnly? from,
            [FromQuery] DateOnly? to,
            CancellationToken cancellationToken)
        {
            var expenses = await mediator.Send(new ListStoreExpensesQuery(from, to), cancellationToken);

            return Ok(new SuccessDetails<IReadOnlyList<StoreExpenseDto>>(
                StatusCodes.Status200OK, "OK", "Despesas da loja carregadas.",
                HttpContext.Request.Path, expenses));
        }

        /// <summary>Lança uma despesa da loja.</summary>
        /// <param name="command">Os dados da despesa.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A despesa lançada.</returns>
        [HttpPost]
        [ProducesResponseType(typeof(SuccessDetails<StoreExpenseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> Create(
            [FromBody] SaveStoreExpenseCommand command,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);

            var expense = await mediator.Send(command with { Code = null }, cancellationToken);

            return Ok(new SuccessDetails<StoreExpenseDto>(
                StatusCodes.Status200OK, "OK", "Despesa lançada.",
                HttpContext.Request.Path, expense));
        }

        /// <summary>Edita uma despesa da loja.</summary>
        /// <param name="code">Identificador público.</param>
        /// <param name="command">Os dados da despesa.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A despesa editada.</returns>
        [HttpPut("{code:guid}")]
        [ProducesResponseType(typeof(SuccessDetails<StoreExpenseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> Update(
            Guid code,
            [FromBody] SaveStoreExpenseCommand command,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);

            var expense = await mediator.Send(command with { Code = code }, cancellationToken);

            return Ok(new SuccessDetails<StoreExpenseDto>(
                StatusCodes.Status200OK, "OK", "Despesa atualizada.",
                HttpContext.Request.Path, expense));
        }

        /// <summary>
        /// Dá baixa numa despesa, ou desfaz a baixa. Sem corpo, paga hoje: é o clique da tela.
        /// </summary>
        /// <param name="code">Identificador público.</param>
        /// <param name="command">Se pagou, e quando. Opcional.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>No content.</returns>
        [HttpPatch("{code:guid}/payment")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Pay(
            Guid code,
            [FromBody] PayStoreExpenseCommand? command,
            CancellationToken cancellationToken)
        {
            await mediator.Send(
                (command ?? new PayStoreExpenseCommand(code)) with { Code = code },
                cancellationToken);

            return NoContent();
        }

        /// <summary>Exclui uma despesa da loja, logicamente.</summary>
        /// <param name="code">Identificador público.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>No content.</returns>
        [HttpDelete("{code:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(Guid code, CancellationToken cancellationToken)
        {
            await mediator.Send(new DeleteStoreExpenseCommand(code), cancellationToken);

            return NoContent();
        }
    }
}
