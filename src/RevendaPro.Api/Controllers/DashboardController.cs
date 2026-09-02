using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RevendaPro.Api.Authorization;
using RevendaPro.Api.Contracts;
using RevendaPro.Application.Dashboard.DTOs;
using RevendaPro.Application.Dashboard.Queries;

namespace RevendaPro.Api.Controllers
{
    /// <summary>The numbers of the operation (RF-23, RF-24), summed at the moment of the request.</summary>
    [ApiController]
    [Route("api/dashboard")]
    [Authorize]
    [RequireScreen("dashboard")]
    public sealed class DashboardController(IMediator mediator) : ControllerBase
    {
        /// <summary>The dashboard. The period bounds only what is realized.</summary>
        /// <param name="from">First day, inclusive.</param>
        /// <param name="to">Last day, inclusive.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The dashboard.</returns>
        [HttpGet]
        [ProducesResponseType(typeof(SuccessDetails<DashboardDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Get(
            [FromQuery] DateOnly? from,
            [FromQuery] DateOnly? to,
            CancellationToken cancellationToken)
        {
            var dashboard = await mediator.Send(new GetDashboardQuery(from, to), cancellationToken);

            return Ok(new SuccessDetails<DashboardDto>(
                StatusCodes.Status200OK, "OK", "Painel carregado.",
                HttpContext.Request.Path, dashboard));
        }
    }

    /// <summary>The sales of the tenant, as a listing (RF-23). Guarded by the sales screen.</summary>
    [ApiController]
    [Route("api/sales")]
    [Authorize]
    [RequireScreen("sales")]
    public sealed class SalesListingController(IMediator mediator) : ControllerBase
    {
        /// <summary>Sales in a period, newest first, each with what it left.</summary>
        /// <param name="from">First day, inclusive.</param>
        /// <param name="to">Last day, inclusive.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The sales.</returns>
        [HttpGet]
        [ProducesResponseType(typeof(SuccessDetails<IReadOnlyList<SaleListingDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> List(
            [FromQuery] DateOnly? from,
            [FromQuery] DateOnly? to,
            CancellationToken cancellationToken)
        {
            var sales = await mediator.Send(new ListSalesQuery(from, to), cancellationToken);

            return Ok(new SuccessDetails<IReadOnlyList<SaleListingDto>>(
                StatusCodes.Status200OK, "OK", "Vendas carregadas.",
                HttpContext.Request.Path, sales));
        }
    }
}
