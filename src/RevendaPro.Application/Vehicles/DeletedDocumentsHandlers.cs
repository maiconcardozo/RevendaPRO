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

namespace RevendaPro.Application.Vehicles.Handlers
{
    /// <summary>
    /// The documents somebody deleted, and the way back.
    ///
    /// Since the M6 the DELETE of a document takes it out of the file of the vehicle and keeps
    /// the object in the bucket, because a dealership answers for what it sold years later.
    /// That left a file paid for and unreachable: it was there, and nobody could ask for it.
    /// This is the door.
    /// </summary>
    public class ListDeletedDocumentsHandler(
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IFileStorage storage)
        : IRequestHandler<ListDeletedDocumentsQuery, IReadOnlyList<DeletedDocumentDto>>
    {
        /// <inheritdoc/>
        public async Task<IReadOnlyList<DeletedDocumentDto>> Handle(
            ListDeletedDocumentsQuery request,
            CancellationToken cancellationToken)
        {
            var documents = await unitOfWork.VehicleDocumentRepository
                .ListDeletedByTenantAsync(currentUser.IdTenant, cancellationToken)
                .ConfigureAwait(false);

            if (documents.Count == 0)
            {
                return [];
            }

            // The name of whoever deleted it, including people who have left: the users are
            // read once, and never one query per row.
            var people = await unitOfWork.UserRepository
                .ListByTenantAsync(currentUser.IdTenant, null, includeDeleted: true, cancellationToken)
                .ConfigureAwait(false);

            var names = people
                .GroupBy(user => user.Code.ToString(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.First().Name,
                    StringComparer.OrdinalIgnoreCase);

            return [.. documents.Select(document => new DeletedDocumentDto(
                document.Code,
                document.Kind,
                document.FileName,
                document.ContentType,
                document.SizeInBytes,
                document.UploadedAt,
                document.DeletedAt,
                document.DeletedByCode is not null
                    && names.TryGetValue(document.DeletedByCode, out var name) ? name : null,
                document.VehicleCode,
                document.Plate,
                document.Brand,
                document.Model,
                storage.GetUrl(document.StorageKey, FileVisibility.Private).ToString()))];
        }
    }

    /// <summary>Puts a deleted document back into the file of its vehicle.</summary>
    public class RestoreVehicleDocumentHandler(IUnitOfWork unitOfWork, ICurrentUser currentUser)
        : IRequestHandler<RestoreVehicleDocumentCommand>
    {
        /// <inheritdoc/>
        public async Task Handle(
            RestoreVehicleDocumentCommand request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var document = await unitOfWork.VehicleDocumentRepository
                .GetByCodeIncludingDeletedAsync(request.Code, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new NotFoundException("Documento inexistente.");

            // The document carries no tenant of its own: it hangs from the vehicle, and the
            // vehicle is what says whose it is. Reading it through the tenant is what keeps a
            // document of another dealership out of reach (RNF-04).
            var vehicle = await unitOfWork.VehicleRepository
                .GetByIdAsync(document.IdVehicle, cancellationToken)
                .ConfigureAwait(false);

            if (vehicle is null || vehicle.IdTenant != currentUser.IdTenant)
            {
                throw new NotFoundException("Documento inexistente.");
            }

            if (document.IsActive)
            {
                throw new BusinessRuleException("Este documento já está na ficha do veículo.");
            }

            document.Activate(currentUser.Code.ToString());

            unitOfWork.VehicleDocumentRepository.Update(document);

            unitOfWork.AuditLogRepository.Add(AuditLog.Create(
                currentUser.IdTenant, currentUser.Id, nameof(VehicleDocument), document.Code,
                AuditAction.Activate, oldValues: null, newValues: null));

            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
