using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RevendaPro.Api.Authorization;
using RevendaPro.Api.Contracts;
using RevendaPro.Application.Vehicles.Commands;
using RevendaPro.Application.Vehicles.DTOs;
using RevendaPro.Application.Vehicles.Queries;

namespace RevendaPro.Api.Controllers
{
    /// <summary>
    /// Kinds of expense, maintained by the dealership (RF-09).
    ///
    /// The listing is guarded by the vehicles screen, and not by its own: whoever records an
    /// expense has to see the list to pick from it. Changing the list is what needs the
    /// administration screen, and each writing action says so.
    /// </summary>
    [ApiController]
    [Route("api/expense-types")]
    [Authorize]
    [RequireScreen("vehicles")]
    public sealed class ExpenseTypesController(IMediator mediator) : ControllerBase
    {
        /// <summary>Lists the kinds of expense of the tenant.</summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The kinds.</returns>
        [HttpGet]
        [ProducesResponseType(
            typeof(SuccessDetails<IReadOnlyList<ExpenseTypeDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> List(CancellationToken cancellationToken)
        {
            var types = await mediator.Send(new ListExpenseTypesQuery(), cancellationToken);

            return Ok(new SuccessDetails<IReadOnlyList<ExpenseTypeDto>>(
                StatusCodes.Status200OK, "OK", "Tipos de gasto carregados.",
                HttpContext.Request.Path, types));
        }

        /// <summary>Creates a kind of expense.</summary>
        /// <param name="command">Name, keywords and position.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The created kind.</returns>
        [HttpPost]
        [RequireScreen("expense-types")]
        [ProducesResponseType(typeof(SuccessDetails<ExpenseTypeDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> Create(
            [FromBody] SaveExpenseTypeCommand command,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);

            var type = await mediator.Send(command with { Code = null }, cancellationToken);

            return Ok(new SuccessDetails<ExpenseTypeDto>(
                StatusCodes.Status200OK, "OK", "Tipo de gasto criado com sucesso.",
                HttpContext.Request.Path, type));
        }

        /// <summary>Renames a kind of expense or changes its words.</summary>
        /// <param name="code">Public identifier.</param>
        /// <param name="command">Name, keywords and position.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The kind.</returns>
        [HttpPut("{code:guid}")]
        [RequireScreen("expense-types")]
        [ProducesResponseType(typeof(SuccessDetails<ExpenseTypeDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update(
            Guid code,
            [FromBody] SaveExpenseTypeCommand command,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);

            var type = await mediator.Send(command with { Code = code }, cancellationToken);

            return Ok(new SuccessDetails<ExpenseTypeDto>(
                StatusCodes.Status200OK, "OK", "Tipo de gasto atualizado com sucesso.",
                HttpContext.Request.Path, type));
        }

        /// <summary>Soft deletes a kind of expense that no expense uses.</summary>
        /// <param name="code">Public identifier.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>No content.</returns>
        [HttpDelete("{code:guid}")]
        [RequireScreen("expense-types")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> Delete(Guid code, CancellationToken cancellationToken)
        {
            await mediator.Send(new DeleteExpenseTypeCommand(code), cancellationToken);

            return NoContent();
        }
    }
}
