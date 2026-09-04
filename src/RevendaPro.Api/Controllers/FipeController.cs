using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RevendaPro.Api.Authorization;
using RevendaPro.Api.Contracts;
using RevendaPro.Application.Vehicles.DTOs;
using RevendaPro.Application.Vehicles.Queries;

namespace RevendaPro.Api.Controllers
{
    /// <summary>
    /// The reference table, browsed: brand, then model, then year.
    ///
    /// It exists for the car nobody has a code for. Three choices turn a Cruze into
    /// <c>004380-0</c> and <c>2014-5</c>, and from then on every lookup is a direct call.
    ///
    /// Guarded by the vehicles screen, because that is what these lists are for. Nothing here
    /// reads or writes a row of this dealership: they are three passthroughs to the public
    /// table. See ADR-0005.
    /// </summary>
    [ApiController]
    [Route("api/fipe")]
    [Authorize]
    [RequireScreen("vehicles")]
    public sealed class FipeController(IMediator mediator) : ControllerBase
    {
        /// <summary>Every brand the table prices.</summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The brands.</returns>
        [HttpGet("brands")]
        [ProducesResponseType(typeof(SuccessDetails<IReadOnlyList<FipeOptionDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> Brands(CancellationToken cancellationToken)
        {
            var brands = await mediator.Send(new ListFipeBrandsQuery(), cancellationToken);

            return Ok(new SuccessDetails<IReadOnlyList<FipeOptionDto>>(
                StatusCodes.Status200OK, "OK", "Marcas carregadas.",
                HttpContext.Request.Path, brands));
        }

        /// <summary>Every model of one brand.</summary>
        /// <param name="brand">Code of the brand.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The models.</returns>
        [HttpGet("brands/{brand}/models")]
        [ProducesResponseType(typeof(SuccessDetails<IReadOnlyList<FipeOptionDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> Models(string brand, CancellationToken cancellationToken)
        {
            var models = await mediator.Send(new ListFipeModelsQuery(brand), cancellationToken);

            return Ok(new SuccessDetails<IReadOnlyList<FipeOptionDto>>(
                StatusCodes.Status200OK, "OK", "Modelos carregados.",
                HttpContext.Request.Path, models));
        }

        /// <summary>Every year and fuel combination of one model.</summary>
        /// <param name="brand">Code of the brand.</param>
        /// <param name="model">Code of the model.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The options.</returns>
        [HttpGet("brands/{brand}/models/{model}/years")]
        [ProducesResponseType(typeof(SuccessDetails<IReadOnlyList<FipeOptionDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> Years(
            string brand,
            string model,
            CancellationToken cancellationToken)
        {
            var years = await mediator.Send(
                new ListFipeModelYearsQuery(brand, model), cancellationToken);

            return Ok(new SuccessDetails<IReadOnlyList<FipeOptionDto>>(
                StatusCodes.Status200OK, "OK", "Anos carregados.",
                HttpContext.Request.Path, years));
        }
    }
}
