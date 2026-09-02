using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using RevendaPro.Api.Authorization;
using RevendaPro.Api.Contracts;
using RevendaPro.Application.Vehicles.Commands;
using RevendaPro.Application.Vehicles.DTOs;
using RevendaPro.Application.Vehicles.Queries;
using RevendaPro.Domain.Enums;
using RevendaPro.Shared.Exceptions;
using RevendaPro.Shared.Settings;

namespace RevendaPro.Api.Controllers
{
    /// <summary>
    /// Photos and documents of a vehicle (RF-12 and RF-13).
    ///
    /// The file passes through the API, and the browser never talks to the store: it is here
    /// that the tenant is checked, that the content is judged by its first bytes and that the
    /// size limit applies. A direct upload would hand the client a key and trust it. See
    /// ADR-0004.
    ///
    /// Nothing here answers with an address that lasts: every URL is signed and expires, which
    /// is what RNF-06 asks for.
    /// </summary>
    [ApiController]
    [Route("api/vehicles/{code:guid}")]
    [Authorize]
    [RequireScreen("vehicles")]
    public sealed class VehicleFilesController(
        IMediator mediator,
        IOptions<StorageSettings> storageSettings) : ControllerBase
    {
        private readonly StorageSettings settings = storageSettings.Value;

        /// <summary>Lists the photos of a vehicle, in gallery order.</summary>
        /// <param name="code">Public identifier of the vehicle.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The photos.</returns>
        [HttpGet("photos")]
        [ProducesResponseType(
            typeof(SuccessDetails<IReadOnlyList<VehiclePhotoDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ListPhotos(Guid code, CancellationToken cancellationToken)
        {
            var photos = await mediator.Send(new ListVehiclePhotosQuery(code), cancellationToken);

            return Ok(new SuccessDetails<IReadOnlyList<VehiclePhotoDto>>(
                StatusCodes.Status200OK, "OK", "Fotos carregadas.",
                HttpContext.Request.Path, photos));
        }

        /// <summary>Stores a photo of a vehicle.</summary>
        /// <param name="code">Public identifier of the vehicle.</param>
        /// <param name="file">The image.</param>
        /// <param name="kind">What the photo is for.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The stored photo, with its three addresses.</returns>
        [HttpPost("photos")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(typeof(SuccessDetails<VehiclePhotoDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> UploadPhoto(
            Guid code,
            IFormFile file,
            [FromForm] VehiclePhotoKind kind,
            CancellationToken cancellationToken)
        {
            RefuseWhatIsTooLarge(file);

            await using var content = file.OpenReadStream();

            var photo = await mediator.Send(
                new UploadVehiclePhotoCommand(code, kind, content), cancellationToken);

            return Ok(new SuccessDetails<VehiclePhotoDto>(
                StatusCodes.Status200OK, "OK", "Foto enviada com sucesso.",
                HttpContext.Request.Path, photo));
        }

        /// <summary>Rearranges the gallery.</summary>
        /// <param name="code">Public identifier of the vehicle.</param>
        /// <param name="request">The photo codes, in the order they should appear.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>No content.</returns>
        [HttpPatch("photos/order")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ReorderPhotos(
            Guid code,
            [FromBody] ReorderPhotosRequest request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            await mediator.Send(
                new ReorderVehiclePhotosCommand(code, request.Codes), cancellationToken);

            return NoContent();
        }

        /// <summary>Chooses which photo opens the vehicle in the listing.</summary>
        /// <param name="code">Public identifier of the vehicle.</param>
        /// <param name="request">The photo, or empty to leave the vehicle without a cover.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>No content.</returns>
        [HttpPut("cover")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> SetCover(
            Guid code,
            [FromBody] SetCoverPhotoRequest request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            await mediator.Send(
                new SetVehicleCoverPhotoCommand(code, request.PhotoCode), cancellationToken);

            return NoContent();
        }

        /// <summary>Changes what a photo is for.</summary>
        /// <param name="code">Public identifier of the vehicle.</param>
        /// <param name="photoCode">Public identifier of the photo.</param>
        /// <param name="request">The new kind.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>No content.</returns>
        [HttpPatch("photos/{photoCode:guid}/kind")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ReclassifyPhoto(
            Guid code,
            Guid photoCode,
            [FromBody] PhotoKindRequest request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            await mediator.Send(
                new ReclassifyVehiclePhotoCommand(code, photoCode, request.Kind), cancellationToken);

            return NoContent();
        }

        /// <summary>Removes a photo, bytes included.</summary>
        /// <param name="code">Public identifier of the vehicle.</param>
        /// <param name="photoCode">Public identifier of the photo.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>No content.</returns>
        [HttpDelete("photos/{photoCode:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeletePhoto(
            Guid code,
            Guid photoCode,
            CancellationToken cancellationToken)
        {
            await mediator.Send(new DeleteVehiclePhotoCommand(code, photoCode), cancellationToken);

            return NoContent();
        }

        /// <summary>Lists the documents of a vehicle.</summary>
        /// <param name="code">Public identifier of the vehicle.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The documents.</returns>
        [HttpGet("documents")]
        [ProducesResponseType(
            typeof(SuccessDetails<IReadOnlyList<VehicleDocumentDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ListDocuments(Guid code, CancellationToken cancellationToken)
        {
            var documents = await mediator.Send(
                new ListVehicleDocumentsQuery(code), cancellationToken);

            return Ok(new SuccessDetails<IReadOnlyList<VehicleDocumentDto>>(
                StatusCodes.Status200OK, "OK", "Documentos carregados.",
                HttpContext.Request.Path, documents));
        }

        /// <summary>Stores a document of a vehicle.</summary>
        /// <param name="code">Public identifier of the vehicle.</param>
        /// <param name="file">The file. PDF, JPG or PNG.</param>
        /// <param name="kind">Which kind of document it is.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The stored document.</returns>
        [HttpPost("documents")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(typeof(SuccessDetails<VehicleDocumentDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> UploadDocument(
            Guid code,
            IFormFile file,
            [FromForm] VehicleDocumentKind kind,
            CancellationToken cancellationToken)
        {
            RefuseWhatIsTooLarge(file);

            await using var content = file.OpenReadStream();

            var document = await mediator.Send(
                new UploadVehicleDocumentCommand(code, kind, file.FileName, content),
                cancellationToken);

            return Ok(new SuccessDetails<VehicleDocumentDto>(
                StatusCodes.Status200OK, "OK", "Documento enviado com sucesso.",
                HttpContext.Request.Path, document));
        }

        /// <summary>Changes what a document is.</summary>
        /// <param name="code">Public identifier of the vehicle.</param>
        /// <param name="documentCode">Public identifier of the document.</param>
        /// <param name="request">The new kind.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>No content.</returns>
        [HttpPatch("documents/{documentCode:guid}/kind")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ReclassifyDocument(
            Guid code,
            Guid documentCode,
            [FromBody] DocumentKindRequest request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            await mediator.Send(
                new ReclassifyVehicleDocumentCommand(code, documentCode, request.Kind),
                cancellationToken);

            return NoContent();
        }

        /// <summary>
        /// Takes a document out of the listing. <b>The file stays in the store.</b>
        ///
        /// See <see cref="DeleteVehicleDocumentCommand"/> for why this one deletion behaves
        /// differently from every other in the system.
        /// </summary>
        /// <param name="code">Public identifier of the vehicle.</param>
        /// <param name="documentCode">Public identifier of the document.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>No content.</returns>
        [HttpDelete("documents/{documentCode:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteDocument(
            Guid code,
            Guid documentCode,
            CancellationToken cancellationToken)
        {
            await mediator.Send(
                new DeleteVehicleDocumentCommand(code, documentCode), cancellationToken);

            return NoContent();
        }

        /// <summary>
        /// Refuses an oversized file before a single byte reaches the store.
        ///
        /// The limit is configuration and not a constant, because RNF-09 asks for it: a photo
        /// from a new phone weighs several times one from an old phone, and the number moves
        /// with time while nothing else here does.
        /// </summary>
        /// <param name="file">The uploaded file.</param>
        private void RefuseWhatIsTooLarge(IFormFile? file)
        {
            if (file is null || file.Length == 0)
            {
                throw new BusinessRuleException("Selecione um arquivo para enviar.");
            }

            if (file.Length > settings.MaxUploadSizeInBytes)
            {
                var megabytes = settings.MaxUploadSizeInBytes / (1024d * 1024d);

                throw new PayloadTooLargeException($"Envie um arquivo de até {megabytes:0.#} MB.");
            }
        }
    }
}
