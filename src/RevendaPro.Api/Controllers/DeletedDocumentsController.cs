using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RevendaPro.Api.Authorization;
using RevendaPro.Api.Contracts;
using RevendaPro.Application.Vehicles.Commands;
using RevendaPro.Application.Vehicles.DTOs;
using RevendaPro.Application.Vehicles.Queries;

namespace RevendaPro.Api.Controllers
{
    /// <summary>
    /// The documents that left the file of a vehicle, and the way back.
    ///
    /// There is no endpoint here to erase one for good, and the absence is deliberate: the
    /// business asked for documents to be kept forever, the object never left the bucket, and
    /// a definitive delete would undo both that and the administrative recovery of RNF-08.
    ///
    /// Behind a screen of its own, so it is a permission of its own (ADR-0002): it shows
    /// exactly what every other reading of the system hides.
    /// </summary>
    [ApiController]
    [Route("api/deleted-documents")]
    [Authorize]
    [RequireScreen("deleted-documents")]
    public sealed class DeletedDocumentsController(IMediator mediator) : ControllerBase
    {
        /// <summary>Lists the deleted documents of the dealership, newest deletion first.</summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The deleted documents, each with a signed address of the file.</returns>
        [HttpGet]
        [ProducesResponseType(
            typeof(SuccessDetails<IReadOnlyList<DeletedDocumentDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> List(CancellationToken cancellationToken)
        {
            var documents = await mediator.Send(new ListDeletedDocumentsQuery(), cancellationToken);

            return Ok(new SuccessDetails<IReadOnlyList<DeletedDocumentDto>>(
                StatusCodes.Status200OK, "OK", "Documentos excluídos carregados.",
                HttpContext.Request.Path, documents));
        }

        /// <summary>Puts a deleted document back into the file of its vehicle.</summary>
        /// <param name="code">Public identifier of the document.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>No content.</returns>
        [HttpPost("{code:guid}/restore")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> Restore(Guid code, CancellationToken cancellationToken)
        {
            await mediator.Send(new RestoreVehicleDocumentCommand(code), cancellationToken);

            return NoContent();
        }
    }
}
