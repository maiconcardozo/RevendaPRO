using MediatR;
using RevendaPro.Application.Vehicles.Commands;
using RevendaPro.Application.Vehicles.DTOs;
using RevendaPro.Application.Vehicles.Queries;
using RevendaPro.Domain.Entities;
using RevendaPro.Domain.Enums;
using RevendaPro.Domain.Interfaces;
using RevendaPro.Domain.Interfaces.Security;
using RevendaPro.Domain.Interfaces.Storage;
using RevendaPro.Shared.Exceptions;
using RevendaPro.Shared.Helpers;

namespace RevendaPro.Application.Vehicles.Handlers
{
    /// <summary>
    /// How a file of a vehicle is addressed in the store.
    ///
    /// The name the file arrived with never becomes a key: it carries accents, spaces and
    /// whatever the sender decided. The key is derived from the code, which is a UUID v7.
    ///
    /// The tenant comes first so that removing everything of one company, or applying a
    /// lifecycle rule to it, is a prefix operation. See ADR-0004.
    /// </summary>
    internal static class VehicleStorageKeys
    {
        /// <summary>Prefix shared by the three renditions of a photo.</summary>
        /// <param name="idTenant">Owning tenant.</param>
        /// <param name="vehicleCode">Public identifier of the vehicle.</param>
        /// <param name="photoCode">Public identifier of the photo.</param>
        /// <returns>The prefix.</returns>
        public static string Photo(int idTenant, Guid vehicleCode, Guid photoCode) =>
            $"{idTenant}/vehicles/{vehicleCode}/{photoCode}";

        /// <summary>Full key of one rendition.</summary>
        /// <param name="prefix">Prefix of the photo.</param>
        /// <param name="size">Which rendition.</param>
        /// <returns>The key.</returns>
        public static string Rendition(string prefix, ImageSize size) =>
            $"{prefix}-{size.ToString().ToLowerInvariant()}.webp";

        /// <summary>Full key of a document.</summary>
        /// <param name="idTenant">Owning tenant.</param>
        /// <param name="vehicleCode">Public identifier of the vehicle.</param>
        /// <param name="documentCode">Public identifier of the document.</param>
        /// <param name="contentType">Media type, which decides the extension.</param>
        /// <returns>The key.</returns>
        public static string Document(
            int idTenant,
            Guid vehicleCode,
            Guid documentCode,
            string contentType) =>
            $"{idTenant}/vehicles/{vehicleCode}/documents/{documentCode}{ExtensionOf(contentType)}";

        private static string ExtensionOf(string contentType) => contentType switch
        {
            "application/pdf" => ".pdf",
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            _ => string.Empty
        };
    }

    /// <summary>Turns photos and documents into what the screen reads.</summary>
    internal static class VehicleFileMapper
    {
        /// <summary>Builds the DTO of one photo, with the three signed addresses.</summary>
        /// <param name="photo">The photo.</param>
        /// <param name="isCover">Whether it is the cover of the vehicle.</param>
        /// <param name="storage">Where the files live.</param>
        /// <returns>The photo as the screen reads it.</returns>
        public static VehiclePhotoDto ToDto(VehiclePhoto photo, bool isCover, IFileStorage storage) =>
            new(photo.Code,
                photo.Kind,
                photo.Position,
                isCover,
                photo.Width,
                photo.Height,
                photo.SizeInBytes,
                Address(storage, photo.StorageKey, ImageSize.Thumbnail),
                Address(storage, photo.StorageKey, ImageSize.Card),
                Address(storage, photo.StorageKey, ImageSize.Full));

        /// <summary>Builds the DTO of one document, with its signed address.</summary>
        /// <param name="document">The document.</param>
        /// <param name="storage">Where the files live.</param>
        /// <returns>The document as the screen reads it.</returns>
        public static VehicleDocumentDto ToDto(VehicleDocument document, IFileStorage storage) =>
            new(document.Code,
                document.Kind,
                document.FileName,
                document.ContentType,
                document.SizeInBytes,
                document.DtCreated,
                storage.GetUrl(document.StorageKey, FileVisibility.Private).ToString());

        private static string Address(IFileStorage storage, string prefix, ImageSize size) =>
            storage.GetUrl(
                VehicleStorageKeys.Rendition(prefix, size), FileVisibility.Private).ToString();
    }

    /// <summary>Lists the photos of a vehicle (RF-12).</summary>
    public class ListVehiclePhotosHandler(
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IFileStorage storage)
        : IRequestHandler<ListVehiclePhotosQuery, IReadOnlyList<VehiclePhotoDto>>
    {
        /// <inheritdoc/>
        public async Task<IReadOnlyList<VehiclePhotoDto>> Handle(
            ListVehiclePhotosQuery request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var vehicle = await ExpenseContext
                .VehicleOrRefuseAsync(unitOfWork, currentUser.IdTenant, request.VehicleCode, cancellationToken)
                .ConfigureAwait(false);

            var photos = await unitOfWork.VehiclePhotoRepository
                .ListByVehicleAsync(vehicle.Id, cancellationToken)
                .ConfigureAwait(false);

            return [.. photos.Select(photo =>
                VehicleFileMapper.ToDto(photo, photo.Id == vehicle.IdCoverPhoto, storage))];
        }
    }

    /// <summary>Stores a photo of a vehicle (RF-12).</summary>
    public class UploadVehiclePhotoHandler(
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IFileStorage storage,
        IImageProcessor imageProcessor)
        : IRequestHandler<UploadVehiclePhotoCommand, VehiclePhotoDto>
    {
        /// <inheritdoc/>
        public async Task<VehiclePhotoDto> Handle(
            UploadVehiclePhotoCommand request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var idTenant = currentUser.IdTenant;
            var actor = currentUser.Code.ToString();

            var vehicle = await ExpenseContext
                .VehicleOrRefuseAsync(unitOfWork, idTenant, request.VehicleCode, cancellationToken)
                .ConfigureAwait(false);

            // Refuses what is not an image, applies the orientation, drops the metadata and
            // produces the three renditions. Nothing reaches the store before this passes.
            var processed = await imageProcessor
                .ProcessAsync(request.Content, cancellationToken)
                .ConfigureAwait(false);

            var photoCode = Guid.CreateVersion7();
            var prefix = VehicleStorageKeys.Photo(idTenant, vehicle.Code, photoCode);

            foreach (var rendition in processed.Renditions)
            {
                await storage.SaveAsync(
                    new MemoryStream(rendition.Content),
                    new StorageRequest(
                        VehicleStorageKeys.Rendition(prefix, rendition.Size),
                        "image/webp",
                        FileVisibility.Private),
                    cancellationToken).ConfigureAwait(false);
            }

            var existing = await unitOfWork.VehiclePhotoRepository
                .ListByVehicleAsync(vehicle.Id, cancellationToken)
                .ConfigureAwait(false);

            var photo = VehiclePhoto.Create(
                vehicle.Id,
                request.Kind,
                prefix,
                "image/webp",
                (int)processed.TotalSizeInBytes,
                (short)processed.Width,
                (short)processed.Height,
                existing.Count == 0 ? 0 : existing.Max(p => p.Position) + 1,
                actor);

            // The code was decided before the upload, because it is what addresses the file.
            photo.Code = photoCode;

            unitOfWork.VehiclePhotoRepository.Add(photo);
            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

            // The first photo becomes the cover, so a vehicle never sits in the listing
            // without a picture while one exists.
            if (vehicle.IdCoverPhoto is null)
            {
                var saved = await unitOfWork.VehiclePhotoRepository
                    .GetByCodeAsync(photoCode, cancellationToken)
                    .ConfigureAwait(false);

                if (saved is not null)
                {
                    vehicle.SetCoverPhoto(saved.Id);
                    unitOfWork.VehicleRepository.Update(vehicle);
                    await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
                }
            }

            return VehicleFileMapper.ToDto(photo, vehicle.IdCoverPhoto is null, storage);
        }
    }

    /// <summary>Reorders the gallery, which the dealership curates by hand.</summary>
    public class ReorderVehiclePhotosHandler(IUnitOfWork unitOfWork, ICurrentUser currentUser)
        : IRequestHandler<ReorderVehiclePhotosCommand>
    {
        /// <inheritdoc/>
        public async Task Handle(
            ReorderVehiclePhotosCommand request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var vehicle = await ExpenseContext
                .VehicleOrRefuseAsync(unitOfWork, currentUser.IdTenant, request.VehicleCode, cancellationToken)
                .ConfigureAwait(false);

            var photos = await unitOfWork.VehiclePhotoRepository
                .ListByVehicleAsync(vehicle.Id, cancellationToken)
                .ConfigureAwait(false);

            var actor = currentUser.Code.ToString();
            var byCode = photos.ToDictionary(photo => photo.Code);

            for (var position = 0; position < request.Codes.Count; position++)
            {
                if (!byCode.TryGetValue(request.Codes[position], out var photo))
                {
                    continue;
                }

                photo.Reorder(position, actor);
                unitOfWork.VehiclePhotoRepository.Update(photo);
            }

            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Points the cover of the vehicle at one of its photos.</summary>
    public class SetVehicleCoverPhotoHandler(IUnitOfWork unitOfWork, ICurrentUser currentUser)
        : IRequestHandler<SetVehicleCoverPhotoCommand>
    {
        /// <inheritdoc/>
        public async Task Handle(
            SetVehicleCoverPhotoCommand request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var vehicle = await ExpenseContext
                .VehicleOrRefuseAsync(unitOfWork, currentUser.IdTenant, request.VehicleCode, cancellationToken)
                .ConfigureAwait(false);

            int? idPhoto = null;

            if (request.PhotoCode is not null)
            {
                var photo = await unitOfWork.VehiclePhotoRepository
                    .GetByCodeAsync(request.PhotoCode.Value, cancellationToken)
                    .ConfigureAwait(false);

                if (photo is null || photo.IdVehicle != vehicle.Id)
                {
                    throw new NotFoundException("Foto inexistente.");
                }

                idPhoto = photo.Id;
            }

            vehicle.SetCoverPhoto(idPhoto);
            vehicle.UpdateAuditInfo(currentUser.Code.ToString());

            unitOfWork.VehicleRepository.Update(vehicle);

            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Changes what a photo is for.</summary>
    public class ReclassifyVehiclePhotoHandler(IUnitOfWork unitOfWork, ICurrentUser currentUser)
        : IRequestHandler<ReclassifyVehiclePhotoCommand>
    {
        /// <inheritdoc/>
        public async Task Handle(
            ReclassifyVehiclePhotoCommand request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var vehicle = await ExpenseContext
                .VehicleOrRefuseAsync(unitOfWork, currentUser.IdTenant, request.VehicleCode, cancellationToken)
                .ConfigureAwait(false);

            var photo = await unitOfWork.VehiclePhotoRepository
                .GetByCodeAsync(request.PhotoCode, cancellationToken)
                .ConfigureAwait(false);

            if (photo is null || photo.IdVehicle != vehicle.Id)
            {
                throw new NotFoundException("Foto inexistente.");
            }

            photo.Reclassify(request.Kind, currentUser.Code.ToString());

            unitOfWork.VehiclePhotoRepository.Update(photo);

            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Removes a photo, bytes included.</summary>
    public class DeleteVehiclePhotoHandler(
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IFileStorage storage)
        : IRequestHandler<DeleteVehiclePhotoCommand>
    {
        /// <inheritdoc/>
        public async Task Handle(
            DeleteVehiclePhotoCommand request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var vehicle = await ExpenseContext
                .VehicleOrRefuseAsync(unitOfWork, currentUser.IdTenant, request.VehicleCode, cancellationToken)
                .ConfigureAwait(false);

            var photo = await unitOfWork.VehiclePhotoRepository
                .GetByCodeAsync(request.PhotoCode, cancellationToken)
                .ConfigureAwait(false);

            if (photo is null || photo.IdVehicle != vehicle.Id)
            {
                throw new NotFoundException("Foto inexistente.");
            }

            var actor = currentUser.Code.ToString();

            unitOfWork.VehiclePhotoRepository.Remove(photo, actor);

            // A gallery that keeps every discarded frame grows without limit, and a photo
            // taken out of the advertisement has no second life. The bytes go with the row.
            // A document is the opposite case — see DeleteVehicleDocumentHandler.
            await storage.DeleteByPrefixAsync(photo.StorageKey, FileVisibility.Private, cancellationToken)
                .ConfigureAwait(false);

            if (vehicle.IdCoverPhoto == photo.Id)
            {
                var remaining = await unitOfWork.VehiclePhotoRepository
                    .ListByVehicleAsync(vehicle.Id, cancellationToken)
                    .ConfigureAwait(false);

                var next = remaining.FirstOrDefault(p => p.Id != photo.Id);

                vehicle.SetCoverPhoto(next?.Id);
                unitOfWork.VehicleRepository.Update(vehicle);
            }

            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Lists the documents of a vehicle (RF-13).</summary>
    public class ListVehicleDocumentsHandler(
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IFileStorage storage)
        : IRequestHandler<ListVehicleDocumentsQuery, IReadOnlyList<VehicleDocumentDto>>
    {
        /// <inheritdoc/>
        public async Task<IReadOnlyList<VehicleDocumentDto>> Handle(
            ListVehicleDocumentsQuery request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var vehicle = await ExpenseContext
                .VehicleOrRefuseAsync(unitOfWork, currentUser.IdTenant, request.VehicleCode, cancellationToken)
                .ConfigureAwait(false);

            var documents = await unitOfWork.VehicleDocumentRepository
                .ListByVehicleAsync(vehicle.Id, cancellationToken)
                .ConfigureAwait(false);

            return [.. documents.Select(document => VehicleFileMapper.ToDto(document, storage))];
        }
    }

    /// <summary>Stores a document of a vehicle (RF-13).</summary>
    public class UploadVehicleDocumentHandler(
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IFileStorage storage)
        : IRequestHandler<UploadVehicleDocumentCommand, VehicleDocumentDto>
    {
        /// <inheritdoc/>
        public async Task<VehicleDocumentDto> Handle(
            UploadVehicleDocumentCommand request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var idTenant = currentUser.IdTenant;

            var vehicle = await ExpenseContext
                .VehicleOrRefuseAsync(unitOfWork, idTenant, request.VehicleCode, cancellationToken)
                .ConfigureAwait(false);

            using var buffer = new MemoryStream();
            await request.Content.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);

            if (buffer.Length == 0)
            {
                throw new BusinessRuleException("Envie um arquivo com conteúdo.");
            }

            // Judged by the content, and never by the extension or the declared media type,
            // both of which whoever uploads chooses.
            var contentType = ImageFormats.DetectDocument(buffer.ToArray());

            if (contentType.Length == 0)
            {
                throw new BusinessRuleException("Envie um arquivo PDF, JPG ou PNG.");
            }

            var documentCode = Guid.CreateVersion7();
            var key = VehicleStorageKeys.Document(idTenant, vehicle.Code, documentCode, contentType);

            buffer.Position = 0;

            await storage.SaveAsync(
                buffer,
                new StorageRequest(key, contentType, FileVisibility.Private),
                cancellationToken).ConfigureAwait(false);

            var document = VehicleDocument.Create(
                vehicle.Id, request.Kind, key, NameToShow(request.FileName), contentType,
                (int)buffer.Length, currentUser.Code.ToString());

            document.Code = documentCode;

            unitOfWork.VehicleDocumentRepository.Add(document);

            unitOfWork.AuditLogRepository.Add(AuditLog.Create(
                idTenant, currentUser.Id, nameof(VehicleDocument), document.Code,
                AuditAction.Create, oldValues: null, newValues: null));

            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

            return VehicleFileMapper.ToDto(document, storage);
        }

        /// <summary>
        /// The name that goes on the screen, tamed.
        ///
        /// Whoever uploads decides this string, so it arrives as anything: a full path from an
        /// older browser, or three hundred characters that the column refuses. Only the last
        /// segment is kept, and only as much of it as fits, so a careless name becomes a
        /// readable label instead of a database error.
        /// </summary>
        /// <param name="fileName">The name the file arrived with.</param>
        /// <returns>The name to show.</returns>
        private static string NameToShow(string? fileName)
        {
            const int LongestName = 160;

            var name = (fileName ?? string.Empty)
                .Replace('\\', '/')
                .Split('/')[^1]
                .Trim();

            if (name.Length == 0)
            {
                return "documento";
            }

            return name.Length <= LongestName ? name : name[..LongestName];
        }
    }

    /// <summary>Changes what a document is.</summary>
    public class ReclassifyVehicleDocumentHandler(IUnitOfWork unitOfWork, ICurrentUser currentUser)
        : IRequestHandler<ReclassifyVehicleDocumentCommand>
    {
        /// <inheritdoc/>
        public async Task Handle(
            ReclassifyVehicleDocumentCommand request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var vehicle = await ExpenseContext
                .VehicleOrRefuseAsync(unitOfWork, currentUser.IdTenant, request.VehicleCode, cancellationToken)
                .ConfigureAwait(false);

            var document = await unitOfWork.VehicleDocumentRepository
                .GetByCodeAsync(request.DocumentCode, cancellationToken)
                .ConfigureAwait(false);

            if (document is null || document.IdVehicle != vehicle.Id)
            {
                throw new NotFoundException("Documento inexistente.");
            }

            document.Reclassify(request.Kind, currentUser.Code.ToString());

            unitOfWork.VehicleDocumentRepository.Update(document);

            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Takes a document out of the listing, and <b>keeps the file in the store, always</b>.
    ///
    /// This is deliberate, and it is the one place where deleting behaves differently from
    /// everywhere else in this system. A document is fiscal and legal evidence — a sale
    /// invoice, a registration certificate, an auction paper, a payment receipt — and it can be
    /// demanded years later, for a car sold long ago. Somebody tidying a screen is not somebody
    /// deciding to destroy evidence, and the two must never be the same click.
    ///
    /// The row is soft deleted, so an administrator brings it back (RNF-08), and the bytes
    /// stay where they are regardless.
    /// </summary>
    public class DeleteVehicleDocumentHandler(IUnitOfWork unitOfWork, ICurrentUser currentUser)
        : IRequestHandler<DeleteVehicleDocumentCommand>
    {
        /// <inheritdoc/>
        public async Task Handle(
            DeleteVehicleDocumentCommand request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var vehicle = await ExpenseContext
                .VehicleOrRefuseAsync(unitOfWork, currentUser.IdTenant, request.VehicleCode, cancellationToken)
                .ConfigureAwait(false);

            var document = await unitOfWork.VehicleDocumentRepository
                .GetByCodeAsync(request.DocumentCode, cancellationToken)
                .ConfigureAwait(false);

            if (document is null || document.IdVehicle != vehicle.Id)
            {
                throw new NotFoundException("Documento inexistente.");
            }

            unitOfWork.VehicleDocumentRepository.Remove(document, currentUser.Code.ToString());

            unitOfWork.AuditLogRepository.Add(AuditLog.Create(
                currentUser.IdTenant, currentUser.Id, nameof(VehicleDocument), document.Code,
                AuditAction.Delete, oldValues: null, newValues: null));

            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

            // No call to the storage. On purpose.
        }
    }
}
