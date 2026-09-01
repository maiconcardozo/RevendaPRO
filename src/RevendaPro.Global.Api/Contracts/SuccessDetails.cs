namespace RevendaPro.Global.Api.Contracts
{
    /// <summary>
    /// Envelope for a successful response, mirroring ProblemDetails.
    ///
    /// Keys are English; Detail is Portuguese, because the frontend shows it to the user.
    /// See docs/api/responses.md and ADR-0003.
    /// </summary>
    /// <typeparam name="T">Type of the payload.</typeparam>
    /// <param name="Status">HTTP status code.</param>
    /// <param name="Title">Short outcome label.</param>
    /// <param name="Detail">Message the frontend can display.</param>
    /// <param name="Instance">Path that produced the response.</param>
    /// <param name="Data">The payload.</param>
    public sealed record SuccessDetails<T>(
        int Status,
        string Title,
        string Detail,
        string Instance,
        T Data);
}
