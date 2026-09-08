using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RevendaPro.Api.Authorization;
using RevendaPro.Api.Reports;
using RevendaPro.Application.Reports.Queries;

namespace RevendaPro.Api.Controllers
{
    /// <summary>
    /// Os documentos que saem de um carro: a ficha para venda, e a proposta para o cliente.
    ///
    /// Guardados pela tela de veículos, como a ficha do carro na tela: quem vê o carro pode
    /// imprimi-lo. O handler entrega o DTO; a classe em <c>Api/Reports</c> o transforma em
    /// bytes (ADR-0007). Ver <c>docs/plans/m19-relatorios.md</c>.
    /// </summary>
    [ApiController]
    [Route("api/vehicles/{code:guid}/reports")]
    [Authorize]
    [RequireScreen("vehicles")]
    public sealed class ReportsController(IMediator mediator) : ControllerBase
    {
        /// <summary>
        /// A ficha do carro para venda, em PDF: foto, dados, tabela e preço — e nada do que é
        /// da casa.
        /// </summary>
        /// <param name="code">Identificador público do carro.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>O PDF, como anexo.</returns>
        [HttpGet("sale-sheet")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> SaleSheet(Guid code, CancellationToken cancellationToken)
        {
            var sheet = await mediator.Send(new GetSaleSheetQuery(code), cancellationToken);

            return File(
                SaleSheetPdf.Render(sheet),
                SaleSheetPdf.ContentType,
                ReportFileName.For($"Ficha{sheet.Plate}", "pdf"));
        }

        /// <summary>
        /// A proposta registrada, em papel timbrado: a revenda, o cliente, o carro, o valor, a
        /// forma de pagamento, a validade e as assinaturas.
        /// </summary>
        /// <param name="code">Identificador público do carro.</param>
        /// <param name="proposalCode">Identificador público da proposta.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>O PDF, como anexo.</returns>
        [HttpGet("proposals/{proposalCode:guid}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Proposal(Guid code, Guid proposalCode, CancellationToken cancellationToken)
        {
            var proposal = await mediator.Send(new GetProposalDocumentQuery(code, proposalCode), cancellationToken);

            return File(
                ProposalPdf.Render(proposal),
                ProposalPdf.ContentType,
                ReportFileName.For($"Proposta{proposal.Plate}", "pdf"));
        }
    }
}
