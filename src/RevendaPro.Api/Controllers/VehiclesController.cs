using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RevendaPro.Api.Authorization;
using RevendaPro.Api.Contracts;
using RevendaPro.Application.Vehicles.Commands;
using RevendaPro.Application.Vehicles.DTOs;
using RevendaPro.Application.Vehicles.Queries;
using RevendaPro.Domain.Enums;

namespace RevendaPro.Api.Controllers
{
    /// <summary>Vehicles and what was spent on them. Guarded by the vehicles screen.</summary>
    [ApiController]
    [Route("api/vehicles")]
    [Authorize]
    [RequireScreen("vehicles")]
    public sealed class VehiclesController(IMediator mediator) : ControllerBase
    {
        /// <summary>Lists the vehicles of the tenant, with the cost of each one.</summary>
        /// <param name="search">Matches plate, brand, model, version or chassis.</param>
        /// <param name="status">Restricts to one status.</param>
        /// <param name="origin">Restricts to one origin.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The vehicles.</returns>
        [HttpGet]
        [ProducesResponseType(typeof(SuccessDetails<IReadOnlyList<VehicleDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> List(
            [FromQuery] string? search,
            [FromQuery] VehicleStatus? status,
            [FromQuery] VehicleOrigin? origin,
            CancellationToken cancellationToken)
        {
            var vehicles = await mediator.Send(
                new ListVehiclesQuery(search, status, origin), cancellationToken);

            return Ok(new SuccessDetails<IReadOnlyList<VehicleDto>>(
                StatusCodes.Status200OK, "OK", "Veículos carregados.",
                HttpContext.Request.Path, vehicles));
        }

        /// <summary>Reads one vehicle.</summary>
        /// <param name="code">Public identifier.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The vehicle.</returns>
        [HttpGet("{code:guid}")]
        [ProducesResponseType(typeof(SuccessDetails<VehicleDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Get(Guid code, CancellationToken cancellationToken)
        {
            var vehicle = await mediator.Send(new GetVehicleQuery(code), cancellationToken);

            return Ok(new SuccessDetails<VehicleDto>(
                StatusCodes.Status200OK, "OK", "Veículo carregado.",
                HttpContext.Request.Path, vehicle));
        }

        /// <summary>Registers a vehicle.</summary>
        /// <param name="command">The vehicle data.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The registered vehicle.</returns>
        [HttpPost]
        [ProducesResponseType(typeof(SuccessDetails<VehicleDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> Create(
            [FromBody] SaveVehicleCommand command,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);

            var vehicle = await mediator.Send(command with { Code = null }, cancellationToken);

            return Ok(new SuccessDetails<VehicleDto>(
                StatusCodes.Status200OK, "OK", "Veículo cadastrado com sucesso.",
                HttpContext.Request.Path, vehicle));
        }

        /// <summary>Edits a vehicle.</summary>
        /// <param name="code">Public identifier.</param>
        /// <param name="command">The vehicle data.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The vehicle.</returns>
        [HttpPut("{code:guid}")]
        [ProducesResponseType(typeof(SuccessDetails<VehicleDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> Update(
            Guid code,
            [FromBody] SaveVehicleCommand command,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);

            var vehicle = await mediator.Send(command with { Code = code }, cancellationToken);

            return Ok(new SuccessDetails<VehicleDto>(
                StatusCodes.Status200OK, "OK", "Veículo atualizado com sucesso.",
                HttpContext.Request.Path, vehicle));
        }

        /// <summary>Moves the vehicle along the pipeline.</summary>
        /// <param name="code">Public identifier.</param>
        /// <param name="command">Target status and reason.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>No content.</returns>
        [HttpPatch("{code:guid}/status")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> ChangeStatus(
            Guid code,
            [FromBody] ChangeVehicleStatusCommand command,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);

            await mediator.Send(command with { Code = code }, cancellationToken);

            return NoContent();
        }

        /// <summary>Reads the pipeline history of one vehicle.</summary>
        /// <param name="code">Public identifier.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The history.</returns>
        [HttpGet("{code:guid}/history")]
        [ProducesResponseType(
            typeof(SuccessDetails<IReadOnlyList<VehicleStatusEntryDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> History(Guid code, CancellationToken cancellationToken)
        {
            var history = await mediator.Send(new GetVehicleHistoryQuery(code), cancellationToken);

            return Ok(new SuccessDetails<IReadOnlyList<VehicleStatusEntryDto>>(
                StatusCodes.Status200OK, "OK", "Histórico carregado.",
                HttpContext.Request.Path, history));
        }

        /// <summary>Soft deletes a vehicle.</summary>
        /// <param name="code">Public identifier.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>No content.</returns>
        [HttpDelete("{code:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(Guid code, CancellationToken cancellationToken)
        {
            await mediator.Send(new DeleteVehicleCommand(code), cancellationToken);

            return NoContent();
        }

        /// <summary>Lists what was spent on a vehicle.</summary>
        /// <param name="code">Public identifier of the vehicle.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The expenses.</returns>
        [HttpGet("{code:guid}/expenses")]
        [ProducesResponseType(
            typeof(SuccessDetails<IReadOnlyList<VehicleExpenseDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Expenses(Guid code, CancellationToken cancellationToken)
        {
            var expenses = await mediator.Send(new ListVehicleExpensesQuery(code), cancellationToken);

            return Ok(new SuccessDetails<IReadOnlyList<VehicleExpenseDto>>(
                StatusCodes.Status200OK, "OK", "Gastos carregados.",
                HttpContext.Request.Path, expenses));
        }

        /// <summary>Records what was spent on a vehicle.</summary>
        /// <param name="code">Public identifier of the vehicle.</param>
        /// <param name="command">The expense.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The recorded expense.</returns>
        [HttpPost("{code:guid}/expenses")]
        [ProducesResponseType(typeof(SuccessDetails<VehicleExpenseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> AddExpense(
            Guid code,
            [FromBody] SaveVehicleExpenseCommand command,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);

            var expense = await mediator.Send(
                command with { Code = null, VehicleCode = code }, cancellationToken);

            return Ok(new SuccessDetails<VehicleExpenseDto>(
                StatusCodes.Status200OK, "OK", "Gasto lançado com sucesso.",
                HttpContext.Request.Path, expense));
        }

        /// <summary>Changes what was spent on a vehicle.</summary>
        /// <param name="code">Public identifier of the vehicle.</param>
        /// <param name="expenseCode">Public identifier of the expense.</param>
        /// <param name="command">The expense.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The expense.</returns>
        [HttpPut("{code:guid}/expenses/{expenseCode:guid}")]
        [ProducesResponseType(typeof(SuccessDetails<VehicleExpenseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateExpense(
            Guid code,
            Guid expenseCode,
            [FromBody] SaveVehicleExpenseCommand command,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);

            var expense = await mediator.Send(
                command with { Code = expenseCode, VehicleCode = code }, cancellationToken);

            return Ok(new SuccessDetails<VehicleExpenseDto>(
                StatusCodes.Status200OK, "OK", "Gasto atualizado com sucesso.",
                HttpContext.Request.Path, expense));
        }

        /// <summary>Turns a planned expense into a paid one.</summary>
        /// <param name="code">Public identifier of the vehicle.</param>
        /// <param name="expenseCode">Public identifier of the expense.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>No content.</returns>
        [HttpPatch("{code:guid}/expenses/{expenseCode:guid}/payment")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ConfirmExpensePayment(
            Guid code,
            Guid expenseCode,
            CancellationToken cancellationToken)
        {
            await mediator.Send(new ConfirmExpensePaymentCommand(expenseCode), cancellationToken);

            return NoContent();
        }

        /// <summary>Soft deletes an expense.</summary>
        /// <param name="code">Public identifier of the vehicle.</param>
        /// <param name="expenseCode">Public identifier of the expense.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>No content.</returns>
        [HttpDelete("{code:guid}/expenses/{expenseCode:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteExpense(
            Guid code,
            Guid expenseCode,
            CancellationToken cancellationToken)
        {
            await mediator.Send(new DeleteVehicleExpenseCommand(expenseCode), cancellationToken);

            return NoContent();
        }

        /// <summary>
        /// Suggests a description and the kind of expense that goes with it, from what this
        /// dealership already wrote.
        /// </summary>
        /// <param name="term">What the user has typed so far.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The suggestions.</returns>
        [HttpGet("expense-suggestions")]
        [ProducesResponseType(
            typeof(SuccessDetails<IReadOnlyList<ExpenseSuggestionDto>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> SuggestExpense(
            [FromQuery] string term,
            CancellationToken cancellationToken)
        {
            var suggestions = await mediator.Send(new SuggestExpenseQuery(term), cancellationToken);

            return Ok(new SuccessDetails<IReadOnlyList<ExpenseSuggestionDto>>(
                StatusCodes.Status200OK, "OK", "Sugestões carregadas.",
                HttpContext.Request.Path, suggestions));
        }
    }
}
