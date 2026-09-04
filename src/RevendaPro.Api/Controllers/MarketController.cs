using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RevendaPro.Api.Authorization;
using RevendaPro.Api.Contracts;
using RevendaPro.Application.Market.DTOs;
using RevendaPro.Application.Market.Queries;

namespace RevendaPro.Api.Controllers
{
    /// <summary>
    /// The dealership against the reference table.
    ///
    /// A screen of its own, and not one more block on the dashboard: the dashboard answers
    /// parked money and profit, and this one answers where each deal stood against the table
    /// of its month. Screen is permission, so it carries its own key. See ADR-0002 and
    /// ADR-0005.
    /// </summary>
    [ApiController]
    [Route("api/market")]
    [Authorize]
    [RequireScreen("market")]
    public sealed class MarketController(IMediator mediator) : ControllerBase
    {
        /// <summary>Everything the screen shows, in one read.</summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The dealership against the table.</returns>
        [HttpGet]
        [ProducesResponseType(typeof(SuccessDetails<MarketOverviewDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Overview(CancellationToken cancellationToken)
        {
            var overview = await mediator.Send(new GetMarketOverviewQuery(), cancellationToken);

            return Ok(new SuccessDetails<MarketOverviewDto>(
                StatusCodes.Status200OK, "OK", "Mercado carregado.",
                HttpContext.Request.Path, overview));
        }
    }
}
