using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RevendaPro.Api.Authorization;
using RevendaPro.Api.Contracts;
using RevendaPro.Application.Sales.Commands;
using RevendaPro.Application.Sales.DTOs;
using RevendaPro.Application.Sales.Queries;
using RevendaPro.Domain.Enums;

namespace RevendaPro.Api.Controllers
{
    /// <summary>
    /// Proposals and the sale of a vehicle (RF-18 to RF-22). Guarded by the sales screen.
    ///
    /// Every profit here is computed by the server, on every read, and the same arithmetic
    /// serves the preview before the sale and the report after it.
    /// </summary>
    [ApiController]
    [Route("api/vehicles/{code:guid}")]
    [Authorize]
    [RequireScreen("sales")]
    public sealed class SalesController(IMediator mediator) : ControllerBase
    {
        /// <summary>Lists the proposals of a vehicle, each with what it would leave.</summary>
        /// <param name="code">Public identifier of the vehicle.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The proposals.</returns>
        [HttpGet("proposals")]
        [ProducesResponseType(typeof(SuccessDetails<IReadOnlyList<ProposalDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ListProposals(Guid code, CancellationToken cancellationToken)
        {
            var proposals = await mediator.Send(new ListProposalsQuery(code), cancellationToken);

            return Ok(new SuccessDetails<IReadOnlyList<ProposalDto>>(
                StatusCodes.Status200OK, "OK", "Propostas carregadas.",
                HttpContext.Request.Path, proposals));
        }

        /// <summary>What a deal would leave, before anything is saved.</summary>
        /// <param name="code">Public identifier of the vehicle.</param>
        /// <param name="amount">The price under consideration.</param>
        /// <param name="channel">Direct, or through a partner store.</param>
        /// <param name="partnerCutPercent">The store's percentage, when agreed that way.</param>
        /// <param name="partnerCutAmount">The store's amount, when agreed that way.</param>
        /// <param name="commission">Commission to a person, if any.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The result.</returns>
        [HttpGet("deal-preview")]
        [ProducesResponseType(typeof(SuccessDetails<DealResultDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> PreviewDeal(
            Guid code,
            [FromQuery] decimal amount,
            [FromQuery] SaleChannel channel = SaleChannel.Direct,
            [FromQuery] decimal? partnerCutPercent = null,
            [FromQuery] decimal? partnerCutAmount = null,
            [FromQuery] decimal commission = 0,
            CancellationToken cancellationToken = default)
        {
            var result = await mediator.Send(
                new PreviewDealQuery(code, amount, channel, partnerCutPercent, partnerCutAmount, commission),
                cancellationToken);

            return Ok(new SuccessDetails<DealResultDto>(
                StatusCodes.Status200OK, "OK", "Simulação calculada.",
                HttpContext.Request.Path, result));
        }

        /// <summary>Records what somebody offered.</summary>
        /// <param name="code">Public identifier of the vehicle.</param>
        /// <param name="command">The proposal.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The proposal, with what it would leave.</returns>
        [HttpPost("proposals")]
        [ProducesResponseType(typeof(SuccessDetails<ProposalDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> RegisterProposal(
            Guid code,
            [FromBody] RegisterProposalCommand command,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);

            var proposal = await mediator.Send(command with { VehicleCode = code }, cancellationToken);

            return Ok(new SuccessDetails<ProposalDto>(
                StatusCodes.Status200OK, "OK", "Proposta registrada.",
                HttpContext.Request.Path, proposal));
        }

        /// <summary>Declines a proposal. It stays on record.</summary>
        /// <param name="code">Public identifier of the vehicle.</param>
        /// <param name="proposalCode">Public identifier of the proposal.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>No content.</returns>
        [HttpPatch("proposals/{proposalCode:guid}/decline")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> DeclineProposal(
            Guid code,
            Guid proposalCode,
            CancellationToken cancellationToken)
        {
            await mediator.Send(new DeclineProposalCommand(code, proposalCode), cancellationToken);

            return NoContent();
        }

        /// <summary>Soft deletes a proposal recorded by mistake.</summary>
        /// <param name="code">Public identifier of the vehicle.</param>
        /// <param name="proposalCode">Public identifier of the proposal.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>No content.</returns>
        [HttpDelete("proposals/{proposalCode:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> DeleteProposal(
            Guid code,
            Guid proposalCode,
            CancellationToken cancellationToken)
        {
            await mediator.Send(new DeleteProposalCommand(code, proposalCode), cancellationToken);

            return NoContent();
        }

        /// <summary>The sale of the vehicle, or nothing while it is on the lot.</summary>
        /// <param name="code">Public identifier of the vehicle.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The sale, or an empty payload.</returns>
        [HttpGet("sale")]
        [ProducesResponseType(typeof(SuccessDetails<SaleDto?>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetSale(Guid code, CancellationToken cancellationToken)
        {
            var sale = await mediator.Send(new GetSaleQuery(code), cancellationToken);

            return Ok(new SuccessDetails<SaleDto?>(
                StatusCodes.Status200OK, "OK",
                sale is null ? "Veículo sem venda registrada." : "Venda carregada.",
                HttpContext.Request.Path, sale));
        }

        /// <summary>Registers the sale. The only way a vehicle reaches "sold".</summary>
        /// <param name="code">Public identifier of the vehicle.</param>
        /// <param name="command">The sale.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The sale, with what was left.</returns>
        [HttpPost("sale")]
        [ProducesResponseType(typeof(SuccessDetails<SaleDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> RegisterSale(
            Guid code,
            [FromBody] RegisterSaleCommand command,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);

            var sale = await mediator.Send(command with { VehicleCode = code }, cancellationToken);

            return Ok(new SuccessDetails<SaleDto>(
                StatusCodes.Status200OK, "OK", "Venda registrada.",
                HttpContext.Request.Path, sale));
        }

        /// <summary>Undoes the sale. The car goes back to the lot; a traded-in car stays in stock.</summary>
        /// <param name="code">Public identifier of the vehicle.</param>
        /// <param name="command">Why.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>No content.</returns>
        [HttpDelete("sale")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> CancelSale(
            Guid code,
            [FromBody] CancelSaleRequest? command,
            CancellationToken cancellationToken)
        {
            await mediator.Send(new CancelSaleCommand(code, command?.Reason), cancellationToken);

            return NoContent();
        }
    }
}
