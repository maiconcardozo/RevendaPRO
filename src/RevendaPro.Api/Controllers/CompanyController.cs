using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RevendaPro.Api.Authorization;
using RevendaPro.Api.Contracts;
using RevendaPro.Application.Company.Commands;
using RevendaPro.Application.Company.DTOs;
using RevendaPro.Application.Company.Queries;

namespace RevendaPro.Api.Controllers
{
    /// <summary>
    /// Os dados da revenda: o que os documentos gerados imprimem em cima — nome, CNPJ, telefone,
    /// e-mail e endereço. Guardado pela tela própria (ADR-0002). Ver
    /// <c>docs/plans/m19-relatorios.md</c>.
    /// </summary>
    [ApiController]
    [Route("api/company")]
    [Authorize]
    [RequireScreen("company")]
    public sealed class CompanyController(IMediator mediator) : ControllerBase
    {
        /// <summary>Os dados da revenda de quem está logado.</summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A revenda.</returns>
        [HttpGet]
        [ProducesResponseType(typeof(SuccessDetails<CompanyDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Get(CancellationToken cancellationToken)
        {
            var company = await mediator.Send(new GetCompanyQuery(), cancellationToken);

            return Ok(new SuccessDetails<CompanyDto>(
                StatusCodes.Status200OK, "OK", "Dados da revenda carregados.",
                HttpContext.Request.Path, company));
        }

        /// <summary>Edita os dados da revenda.</summary>
        /// <param name="command">Os dados.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A revenda editada.</returns>
        [HttpPut]
        [ProducesResponseType(typeof(SuccessDetails<CompanyDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> Save(
            [FromBody] SaveCompanyCommand command,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);

            var company = await mediator.Send(command, cancellationToken);

            return Ok(new SuccessDetails<CompanyDto>(
                StatusCodes.Status200OK, "OK", "Dados da revenda atualizados.",
                HttpContext.Request.Path, company));
        }
    }
}
