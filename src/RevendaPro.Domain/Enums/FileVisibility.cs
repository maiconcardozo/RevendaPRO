namespace RevendaPro.Domain.Enums
{
    /// <summary>
    /// Under which regime a file is served.
    ///
    /// This lives in the domain, and not in a bucket setting, because it is a business rule:
    /// a vehicle photo is born to go in the advertisement, and a document carries personal
    /// data. Leaving the distinction to infrastructure is how a document ends up public by
    /// accident. See ADR-0004.
    /// </summary>
    public enum FileVisibility
    {
        /// <summary>Reachable by a stable address, behind the CDN. Vehicle photos.</summary>
        Public = 1,

        /// <summary>Reachable only through a signed address that expires. Documents.</summary>
        Private = 2
    }
}
