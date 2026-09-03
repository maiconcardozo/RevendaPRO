using MediatR;
using RevendaPro.Domain.Enums;

namespace RevendaPro.Application.Vehicles.DTOs
{
    /// <summary>
    /// A document that left the file of a vehicle, and whose file is still in the bucket.
    /// </summary>
    /// <param name="Code">Public identifier of the document.</param>
    /// <param name="Kind">Which kind of document it is.</param>
    /// <param name="FileName">Name of the file as it was sent.</param>
    /// <param name="ContentType">What the file is.</param>
    /// <param name="SizeInBytes">How big it is.</param>
    /// <param name="UploadedAt">When it was attached.</param>
    /// <param name="DeletedAt">When it left the file of the vehicle.</param>
    /// <param name="DeletedBy">Who deleted it, by name. Null when the name is unknown.</param>
    /// <param name="VehicleCode">Public identifier of the vehicle, so the screen can link.</param>
    /// <param name="Plate">Plate of the vehicle.</param>
    /// <param name="Brand">Brand of the vehicle.</param>
    /// <param name="Model">Model of the vehicle.</param>
    /// <param name="Url">
    /// Signed address of the file, valid for minutes. The object never left the bucket, so it
    /// opens the same way it always did — under authentication, and never by a public link.
    /// </param>
    public sealed record DeletedDocumentDto(
        Guid Code,
        VehicleDocumentKind Kind,
        string FileName,
        string ContentType,
        int SizeInBytes,
        DateTime UploadedAt,
        DateTime? DeletedAt,
        string? DeletedBy,
        Guid VehicleCode,
        string Plate,
        string Brand,
        string Model,
        string Url);
}

namespace RevendaPro.Application.Vehicles.Queries
{
    using RevendaPro.Application.Vehicles.DTOs;

    /// <summary>
    /// Lists the documents deleted in the dealership, newest deletion first.
    ///
    /// Administrative on purpose: the screen shows what every other reading of the system
    /// hides, so it lives behind a screen of its own, which by ADR-0002 is a permission of
    /// its own.
    /// </summary>
    public sealed record ListDeletedDocumentsQuery : IRequest<IReadOnlyList<DeletedDocumentDto>>;
}

namespace RevendaPro.Application.Vehicles.Commands
{
    /// <summary>
    /// Puts a deleted document back into the file of its vehicle.
    ///
    /// There is no command to erase one for good, and that absence is the design: a document
    /// is kept forever by requirement, and the file has been in the bucket the whole time.
    /// </summary>
    /// <param name="Code">Public identifier of the document.</param>
    public sealed record RestoreVehicleDocumentCommand(Guid Code) : IRequest;
}
