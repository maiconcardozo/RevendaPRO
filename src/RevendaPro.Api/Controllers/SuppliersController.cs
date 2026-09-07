using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RevendaPro.Api.Authorization;
using RevendaPro.Api.Contracts;
using RevendaPro.Application.Suppliers.Commands;
using RevendaPro.Application.Suppliers.DTOs;
using RevendaPro.Application.Suppliers.Queries;

namespace RevendaPro.Api.Controllers
{
    /// <summary>
    /// De quem a revenda compra serviço e peça: oficina, funilaria, autopeças, despachante.
    ///
    /// A listagem é guardada pela tela de veículos, e não pela própria: quem registra um gasto
    /// precisa ver a lista para escolher o fornecedor. Mexer no cadastro é o que exige a tela de
    /// administração, e cada ação de escrita diz isso — a mesma divisão do tipo de gasto.
    /// Ver <c>docs/plans/m18-fornecedores.md</c>.
    /// </summary>
    [ApiController]
    [Route("api/suppliers")]
    [Authorize]
    [RequireScreen("vehicles")]
    public sealed class SuppliersController(IMediator mediator) : ControllerBase
    {
        /// <summary>Os fornecedores da revenda, com quantos gastos apontam para cada um.</summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>Os fornecedores.</returns>
        [HttpGet]
        [ProducesResponseType(typeof(SuccessDetails<IReadOnlyList<SupplierDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> List(CancellationToken cancellationToken)
        {
            var suppliers = await mediator.Send(new ListSuppliersQuery(), cancellationToken);

            return Ok(new SuccessDetails<IReadOnlyList<SupplierDto>>(
                StatusCodes.Status200OK, "OK", "Fornecedores carregados.",
                HttpContext.Request.Path, suppliers));
        }

        /// <summary>Cadastra um fornecedor.</summary>
        /// <param name="command">Os dados do fornecedor.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>O fornecedor cadastrado.</returns>
        [HttpPost]
        [RequireScreen("suppliers")]
        [ProducesResponseType(typeof(SuccessDetails<SupplierDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> Create(
            [FromBody] SaveSupplierCommand command,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);

            var supplier = await mediator.Send(command with { Code = null }, cancellationToken);

            return Ok(new SuccessDetails<SupplierDto>(
                StatusCodes.Status200OK, "OK", "Fornecedor cadastrado.",
                HttpContext.Request.Path, supplier));
        }

        /// <summary>Edita um fornecedor.</summary>
        /// <param name="code">Identificador público.</param>
        /// <param name="command">Os dados do fornecedor.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>O fornecedor editado.</returns>
        [HttpPut("{code:guid}")]
        [RequireScreen("suppliers")]
        [ProducesResponseType(typeof(SuccessDetails<SupplierDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> Update(
            Guid code,
            [FromBody] SaveSupplierCommand command,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);

            var supplier = await mediator.Send(command with { Code = code }, cancellationToken);

            return Ok(new SuccessDetails<SupplierDto>(
                StatusCodes.Status200OK, "OK", "Fornecedor atualizado.",
                HttpContext.Request.Path, supplier));
        }

        /// <summary>Exclui um fornecedor, logicamente.</summary>
        /// <param name="code">Identificador público.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>No content.</returns>
        [HttpDelete("{code:guid}")]
        [RequireScreen("suppliers")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> Delete(Guid code, CancellationToken cancellationToken)
        {
            await mediator.Send(new DeleteSupplierCommand(code), cancellationToken);

            return NoContent();
        }
    }

    /// <summary>
    /// Os ramos de fornecedor: oficina, funilaria, autopeças. Cadastro da revenda, guardado
    /// inteiro pela tela de fornecedores — quem mexe em ramo é quem mexe em fornecedor.
    /// </summary>
    [ApiController]
    [Route("api/supplier-segments")]
    [Authorize]
    [RequireScreen("suppliers")]
    public sealed class SupplierSegmentsController(IMediator mediator) : ControllerBase
    {
        /// <summary>Os ramos da revenda, com quantos fornecedores estão em cada um.</summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>Os ramos.</returns>
        [HttpGet]
        [ProducesResponseType(typeof(SuccessDetails<IReadOnlyList<SupplierSegmentDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> List(CancellationToken cancellationToken)
        {
            var segments = await mediator.Send(new ListSupplierSegmentsQuery(), cancellationToken);

            return Ok(new SuccessDetails<IReadOnlyList<SupplierSegmentDto>>(
                StatusCodes.Status200OK, "OK", "Ramos carregados.",
                HttpContext.Request.Path, segments));
        }

        /// <summary>Cadastra um ramo.</summary>
        /// <param name="command">Nome e posição.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>O ramo cadastrado.</returns>
        [HttpPost]
        [ProducesResponseType(typeof(SuccessDetails<SupplierSegmentDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> Create(
            [FromBody] SaveSupplierSegmentCommand command,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);

            var segment = await mediator.Send(command with { Code = null }, cancellationToken);

            return Ok(new SuccessDetails<SupplierSegmentDto>(
                StatusCodes.Status200OK, "OK", "Ramo cadastrado.",
                HttpContext.Request.Path, segment));
        }

        /// <summary>Edita um ramo.</summary>
        /// <param name="code">Identificador público.</param>
        /// <param name="command">Nome e posição.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>O ramo editado.</returns>
        [HttpPut("{code:guid}")]
        [ProducesResponseType(typeof(SuccessDetails<SupplierSegmentDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> Update(
            Guid code,
            [FromBody] SaveSupplierSegmentCommand command,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);

            var segment = await mediator.Send(command with { Code = code }, cancellationToken);

            return Ok(new SuccessDetails<SupplierSegmentDto>(
                StatusCodes.Status200OK, "OK", "Ramo atualizado.",
                HttpContext.Request.Path, segment));
        }

        /// <summary>Exclui um ramo, logicamente.</summary>
        /// <param name="code">Identificador público.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>No content.</returns>
        [HttpDelete("{code:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> Delete(Guid code, CancellationToken cancellationToken)
        {
            await mediator.Send(new DeleteSupplierSegmentCommand(code), cancellationToken);

            return NoContent();
        }
    }
}
