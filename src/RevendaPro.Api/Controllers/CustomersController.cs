using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RevendaPro.Api.Authorization;
using RevendaPro.Api.Contracts;
using RevendaPro.Application.Customers.Commands;
using RevendaPro.Application.Customers.DTOs;
using RevendaPro.Application.Customers.Queries;

namespace RevendaPro.Api.Controllers
{
    /// <summary>
    /// Quem a revenda conhece: quem ofereceu, quem comprou, quem volta (M21).
    ///
    /// A busca é guardada pela tela de vendas, e não pela própria: quem registra uma proposta
    /// precisa achar o cliente para escolher. A ficha e a escrita exigem a tela Clientes — a
    /// mesma divisão do fornecedor com o gasto. Ver <c>docs/plans/m21-clientes.md</c>.
    /// </summary>
    [ApiController]
    [Route("api/customers")]
    [Authorize]
    [RequireScreen("sales")]
    public sealed class CustomersController(IMediator mediator) : ControllerBase
    {
        /// <summary>
        /// Os clientes da revenda, por nome, filtrados por um trecho do nome, do telefone ou do
        /// documento. Sem filtro traz todos.
        /// </summary>
        /// <param name="search">Trecho a procurar.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>Os clientes.</returns>
        [HttpGet]
        [ProducesResponseType(typeof(SuccessDetails<IReadOnlyList<CustomerDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> List([FromQuery] string? search, CancellationToken cancellationToken)
        {
            var customers = await mediator.Send(new ListCustomersQuery(search), cancellationToken);

            return Ok(new SuccessDetails<IReadOnlyList<CustomerDto>>(
                StatusCodes.Status200OK, "OK", "Clientes carregados.",
                HttpContext.Request.Path, customers));
        }

        /// <summary>A ficha de um cliente: os dados e o histórico de propostas e compras.</summary>
        /// <param name="code">Identificador público.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A ficha.</returns>
        [HttpGet("{code:guid}")]
        [RequireScreen("customers")]
        [ProducesResponseType(typeof(SuccessDetails<CustomerDetailDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Get(Guid code, CancellationToken cancellationToken)
        {
            var detail = await mediator.Send(new GetCustomerQuery(code), cancellationToken);

            return Ok(new SuccessDetails<CustomerDetailDto>(
                StatusCodes.Status200OK, "OK", "Ficha do cliente carregada.",
                HttpContext.Request.Path, detail));
        }

        /// <summary>Cadastra um cliente.</summary>
        /// <param name="command">Os dados do cliente.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>O cliente cadastrado.</returns>
        [HttpPost]
        [RequireScreen("customers")]
        [ProducesResponseType(typeof(SuccessDetails<CustomerDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> Create(
            [FromBody] SaveCustomerCommand command,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);

            var customer = await mediator.Send(command with { Code = null }, cancellationToken);

            return Ok(new SuccessDetails<CustomerDto>(
                StatusCodes.Status200OK, "OK", "Cliente cadastrado.",
                HttpContext.Request.Path, customer));
        }

        /// <summary>Edita um cliente.</summary>
        /// <param name="code">Identificador público.</param>
        /// <param name="command">Os dados do cliente.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>O cliente editado.</returns>
        [HttpPut("{code:guid}")]
        [RequireScreen("customers")]
        [ProducesResponseType(typeof(SuccessDetails<CustomerDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> Update(
            Guid code,
            [FromBody] SaveCustomerCommand command,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);

            var customer = await mediator.Send(command with { Code = code }, cancellationToken);

            return Ok(new SuccessDetails<CustomerDto>(
                StatusCodes.Status200OK, "OK", "Cliente atualizado.",
                HttpContext.Request.Path, customer));
        }

        /// <summary>Exclui um cliente, logicamente. Recusado quando ele tem história.</summary>
        /// <param name="code">Identificador público.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>No content.</returns>
        [HttpDelete("{code:guid}")]
        [RequireScreen("customers")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> Delete(Guid code, CancellationToken cancellationToken)
        {
            await mediator.Send(new DeleteCustomerCommand(code), cancellationToken);

            return NoContent();
        }
    }
}
