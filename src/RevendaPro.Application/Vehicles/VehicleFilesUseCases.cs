using RevendaPro.Domain.Enums;

namespace RevendaPro.Application.Vehicles.DTOs
{
    /// <summary>
    /// A photo of a vehicle, with the addresses the browser fetches (RF-12).
    ///
    /// The addresses are signed and expire: nothing here is public, and a link that leaks is
    /// worth little for long. See ADR-0004.
    /// </summary>
    /// <param name="Code">Public identifier.</param>
    /// <param name="Kind">What the photo is for.</param>
    /// <param name="Position">Position in the gallery.</param>
    /// <param name="IsCover">Whether this is the cover of the vehicle.</param>
    /// <param name="Width">Width of the full rendition.</param>
    /// <param name="Height">Height of the full rendition.</param>
    /// <param name="SizeInBytes">The three renditions together.</param>
    /// <param name="ThumbnailUrl">Address of the smallest rendition, for a list.</param>
    /// <param name="CardUrl">Address of the middle rendition, for the gallery.</param>
    /// <param name="FullUrl">Address of the largest rendition.</param>
    public sealed record VehiclePhotoDto(
        Guid Code,
        VehiclePhotoKind Kind,
        int Position,
        bool IsCover,
        short Width,
        short Height,
        int SizeInBytes,
        string ThumbnailUrl,
        string CardUrl,
        string FullUrl);

    /// <summary>A document attached to a vehicle (RF-13).</summary>
    /// <param name="Code">Public identifier.</param>
    /// <param name="Kind">Which kind.</param>
    /// <param name="FileName">Name to show.</param>
    /// <param name="ContentType">Media type.</param>
    /// <param name="SizeInBytes">Size.</param>
    /// <param name="UploadedAt">When it arrived.</param>
    /// <param name="Url">Signed address, of short life.</param>
    public sealed record VehicleDocumentDto(
        Guid Code,
        VehicleDocumentKind Kind,
        string FileName,
        string ContentType,
        int SizeInBytes,
        DateTime UploadedAt,
        string Url);
}

namespace RevendaPro.Application.Vehicles.Queries
{
    using MediatR;
    using RevendaPro.Application.Vehicles.DTOs;

    /// <summary>Lists the photos of a vehicle, in gallery order (RF-12).</summary>
    /// <param name="VehicleCode">Public identifier of the vehicle.</param>
    public sealed record ListVehiclePhotosQuery(Guid VehicleCode)
        : IRequest<IReadOnlyList<VehiclePhotoDto>>;

    /// <summary>Lists the documents of a vehicle (RF-13).</summary>
    /// <param name="VehicleCode">Public identifier of the vehicle.</param>
    public sealed record ListVehicleDocumentsQuery(Guid VehicleCode)
        : IRequest<IReadOnlyList<VehicleDocumentDto>>;
}

namespace RevendaPro.Application.Vehicles.Commands
{
    using MediatR;
    using RevendaPro.Application.Vehicles.DTOs;

    /// <summary>Stores a photo of a vehicle (RF-12).</summary>
    /// <param name="VehicleCode">Public identifier of the vehicle.</param>
    /// <param name="Kind">What the photo is for.</param>
    /// <param name="Content">The uploaded bytes.</param>
    public sealed record UploadVehiclePhotoCommand(
        Guid VehicleCode,
        VehiclePhotoKind Kind,
        Stream Content) : IRequest<VehiclePhotoDto>;

    /// <summary>Reorders the gallery, which the dealership curates by hand.</summary>
    /// <param name="VehicleCode">Public identifier of the vehicle.</param>
    /// <param name="Codes">Photo codes, in the order they should appear.</param>
    public sealed record ReorderVehiclePhotosCommand(Guid VehicleCode, IReadOnlyList<Guid> Codes)
        : IRequest;

    /// <summary>Points the cover of the vehicle at one of its photos.</summary>
    /// <param name="VehicleCode">Public identifier of the vehicle.</param>
    /// <param name="PhotoCode">Photo to use, or null to clear it.</param>
    public sealed record SetVehicleCoverPhotoCommand(Guid VehicleCode, Guid? PhotoCode) : IRequest;

    /// <summary>Changes what a photo is for.</summary>
    /// <param name="VehicleCode">Public identifier of the vehicle.</param>
    /// <param name="PhotoCode">Public identifier of the photo.</param>
    /// <param name="Kind">New kind.</param>
    public sealed record ReclassifyVehiclePhotoCommand(
        Guid VehicleCode,
        Guid PhotoCode,
        VehiclePhotoKind Kind) : IRequest;

    /// <summary>Removes a photo, bytes included.</summary>
    /// <param name="VehicleCode">Public identifier of the vehicle.</param>
    /// <param name="PhotoCode">Public identifier of the photo.</param>
    public sealed record DeleteVehiclePhotoCommand(Guid VehicleCode, Guid PhotoCode) : IRequest;

    /// <summary>Stores a document of a vehicle (RF-13).</summary>
    /// <param name="VehicleCode">Public identifier of the vehicle.</param>
    /// <param name="Kind">Which kind.</param>
    /// <param name="FileName">Name the file arrived with.</param>
    /// <param name="Content">The uploaded bytes.</param>
    public sealed record UploadVehicleDocumentCommand(
        Guid VehicleCode,
        VehicleDocumentKind Kind,
        string FileName,
        Stream Content) : IRequest<VehicleDocumentDto>;

    /// <summary>Changes what a document is.</summary>
    /// <param name="VehicleCode">Public identifier of the vehicle.</param>
    /// <param name="DocumentCode">Public identifier of the document.</param>
    /// <param name="Kind">New kind.</param>
    public sealed record ReclassifyVehicleDocumentCommand(
        Guid VehicleCode,
        Guid DocumentCode,
        VehicleDocumentKind Kind) : IRequest;

    /// <summary>
    /// Takes a document out of the listing.
    ///
    /// <b>The file itself stays in the store, always.</b> A document is fiscal and legal
    /// evidence — a sale invoice, a registration certificate, an auction paper — and it may be
    /// needed years later, for a car sold long ago. What this removes is the row from the
    /// screen, and never the bytes.
    /// </summary>
    /// <param name="VehicleCode">Public identifier of the vehicle.</param>
    /// <param name="DocumentCode">Public identifier of the document.</param>
    public sealed record DeleteVehicleDocumentCommand(Guid VehicleCode, Guid DocumentCode) : IRequest;
}
