using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RevendaPro.Api.Authorization;
using RevendaPro.Api.Contracts;
using RevendaPro.Application.Company.Commands;
using RevendaPro.Application.Company.DTOs;
using RevendaPro.Application.Company.Queries;
using RevendaPro.Shared.Exceptions;

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

        /// <summary>
        /// Troca o logotipo da revenda (M25). O que chega já veio recortado pelo navegador, na
        /// proporção do papel.
        /// </summary>
        /// <param name="file">A imagem.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A revenda, já com o logotipo.</returns>
        [HttpPost("logo")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(4_194_304)]
        [ProducesResponseType(typeof(SuccessDetails<CompanyDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> UploadLogo(IFormFile file, CancellationToken cancellationToken)
        {
            if (file is null || file.Length == 0)
            {
                throw new BusinessRuleException("Selecione uma imagem.");
            }

            await using var content = file.OpenReadStream();

            var company = await mediator.Send(new SaveCompanyLogoCommand(content), cancellationToken);

            return Ok(new SuccessDetails<CompanyDto>(
                StatusCodes.Status200OK, "OK", "Logotipo atualizado.",
                HttpContext.Request.Path, company));
        }

        /// <summary>Remove o logotipo da revenda (M25). Os papéis voltam a sair como antes.</summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A revenda, sem logotipo.</returns>
        [HttpDelete("logo")]
        [ProducesResponseType(typeof(SuccessDetails<CompanyDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> RemoveLogo(CancellationToken cancellationToken)
        {
            var company = await mediator.Send(new RemoveCompanyLogoCommand(), cancellationToken);

            return Ok(new SuccessDetails<CompanyDto>(
                StatusCodes.Status200OK, "OK", "Logotipo removido.",
                HttpContext.Request.Path, company));
        }
    }

    /// <summary>
    /// Serve o logotipo da revenda (M25).
    ///
    /// Sem <c>RequireScreen</c>, de propósito: o logotipo é a identidade da loja, e quem está
    /// dentro dela já a conhece. Trocar e remover continuam exigindo a tela de dados da revenda,
    /// no controlador acima — o mesmo desenho da foto do usuário.
    /// </summary>
    [ApiController]
    [Route("api/company")]
    [Authorize]
    public sealed class CompanyLogoController(IMediator mediator) : ControllerBase
    {
        /// <summary>O logotipo da revenda de quem está logado, ou 404 quando ela não subiu um.</summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A imagem PNG, ou 404.</returns>
        [HttpGet("logo")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Get(CancellationToken cancellationToken)
        {
            var logo = await mediator.Send(new ReadCompanyLogoQuery(), cancellationToken);

            if (logo is null)
            {
                return NotFound();
            }

            // O nome do arquivo muda a cada troca, e a tela pede com a versao na URL: o cache
            // pode ser longo sem nunca mostrar o logotipo antigo.
            Response.Headers.CacheControl = "private, max-age=3600";

            return File(logo.Content, logo.ContentType);
        }
    }
}
