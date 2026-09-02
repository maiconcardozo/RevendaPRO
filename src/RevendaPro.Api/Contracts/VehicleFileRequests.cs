namespace RevendaPro.Api.Contracts
{
    using RevendaPro.Domain.Enums;

    /// <summary>
    /// The gallery in the order the dealership wants it.
    ///
    /// The whole order travels at once, and not one move at a time, because dragging a photo
    /// from the end to the front changes the position of every photo between them. Sending the
    /// final arrangement leaves no half-applied state behind if the connection drops.
    /// </summary>
    /// <param name="Codes">Photo codes, in the order they should appear.</param>
    public sealed record ReorderPhotosRequest(IReadOnlyList<Guid> Codes);

    /// <summary>Which photo opens the vehicle in the listing.</summary>
    /// <param name="PhotoCode">Photo to use, or empty to leave the vehicle without a cover.</param>
    public sealed record SetCoverPhotoRequest(Guid? PhotoCode);

    /// <summary>What a photo is for.</summary>
    /// <param name="Kind">The new kind.</param>
    public sealed record PhotoKindRequest(VehiclePhotoKind Kind);

    /// <summary>What a document is.</summary>
    /// <param name="Kind">The new kind.</param>
    public sealed record DocumentKindRequest(VehicleDocumentKind Kind);

    /// <summary>Why a sale is being undone. Optional, and it goes into the history.</summary>
    /// <param name="Reason">The reason.</param>
    public sealed record CancelSaleRequest(string? Reason);
}
