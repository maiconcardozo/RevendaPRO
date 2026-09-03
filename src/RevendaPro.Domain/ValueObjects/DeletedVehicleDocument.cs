using RevendaPro.Domain.Enums;

namespace RevendaPro.Domain.ValueObjects
{
    /// <summary>
    /// A document that was taken out of the file of a vehicle, and whose file is still in the
    /// bucket.
    ///
    /// Deleting a document was always logical, and the object was deliberately kept in the
    /// store — a dealership answers for what it sold years after selling it, and a receipt
    /// that somebody dropped by mistake on a Tuesday still has to exist on Wednesday. What was
    /// missing is the door back: until now the file sat there, paid for and unreachable.
    ///
    /// Carries the vehicle it belonged to, because the administrator who opens the screen
    /// looks for "the document of the Cruze", and never for a code.
    /// </summary>
    /// <param name="Code">Public identifier of the document.</param>
    /// <param name="Kind">Which kind of document it is.</param>
    /// <param name="FileName">Name of the file as it was sent.</param>
    /// <param name="ContentType">What the file is, judged at upload time.</param>
    /// <param name="SizeInBytes">How big it is.</param>
    /// <param name="StorageKey">Where the object lives, for the signed address.</param>
    /// <param name="UploadedAt">When it was attached.</param>
    /// <param name="DeletedAt">When it left the file of the vehicle.</param>
    /// <param name="DeletedByCode">Who deleted it, as the tables store it: the user code.</param>
    /// <param name="VehicleCode">Public identifier of the vehicle, so the screen can link.</param>
    /// <param name="Plate">Plate of the vehicle.</param>
    /// <param name="Brand">Brand of the vehicle.</param>
    /// <param name="Model">Model of the vehicle.</param>
    public sealed record DeletedVehicleDocument(
        Guid Code,
        VehicleDocumentKind Kind,
        string FileName,
        string ContentType,
        int SizeInBytes,
        string StorageKey,
        DateTime UploadedAt,
        DateTime? DeletedAt,
        string? DeletedByCode,
        Guid VehicleCode,
        string Plate,
        string Brand,
        string Model);
}
