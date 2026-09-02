using RevendaPro.Domain.Enums;

namespace RevendaPro.Domain.Interfaces.Storage
{
    /// <summary>
    /// Where files live, expressed in terms the domain understands.
    ///
    /// The port speaks of <b>addresses</b>, and not only of bytes. A port that could just
    /// hand back content would leave the code independent of the provider on paper, with
    /// every image still travelling through the application — the cost of the abstraction
    /// without its benefit, and the CDN in front of the storage rendered useless.
    ///
    /// No implementation carries a provider name. MinIO, Cloudflare R2 and AWS S3 speak the
    /// same API; what differs between them is configuration. See ADR-0004.
    /// </summary>
    public interface IFileStorage
    {
        /// <summary>
        /// Largest file this store accepts, in bytes (RNF-09).
        ///
        /// It lives on the port because it is a property of the store, and because the screen
        /// has to know it: a browser that checks the size before sending refuses a file
        /// instantly, instead of spending the upload to be told no at the end. The refusal on
        /// the way in stays the real guard.
        /// </summary>
        long MaxSizeInBytes { get; }

        /// <summary>Stores the content and answers where it landed.</summary>
        /// <param name="content">The bytes to store.</param>
        /// <param name="request">Key, content type and visibility.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The stored file, with the key the rest of the system keeps.</returns>
        Task<StoredFile> SaveAsync(
            Stream content,
            StorageRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// The address the browser reaches directly, so the application never carries the
        /// bytes of an image a second time.
        /// </summary>
        /// <param name="key">Key returned by <see cref="SaveAsync"/>.</param>
        /// <param name="visibility">
        /// Public answers a stable address, behind the CDN. Private answers a signed address
        /// that expires.
        /// </param>
        /// <param name="expiresIn">
        /// How long a private address stays valid. Ignored for a public one. Defaults to
        /// fifteen minutes, long enough to open a document and short enough that a leaked
        /// address is worth little.
        /// </param>
        /// <returns>The address to hand to the browser.</returns>
        Uri GetUrl(string key, FileVisibility visibility, TimeSpan? expiresIn = null);

        /// <summary>Removes the file. Removing what is already gone succeeds.</summary>
        /// <param name="key">Key of the file.</param>
        /// <param name="visibility">Which store holds it.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task DeleteAsync(
            string key,
            FileVisibility visibility,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes everything under a prefix. Deleting a vehicle takes its whole gallery with
        /// it, in one operation instead of one call per photo.
        /// </summary>
        /// <param name="prefix">Key prefix, such as the folder of a vehicle.</param>
        /// <param name="visibility">Which store to sweep.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>How many files were removed.</returns>
        Task<int> DeleteByPrefixAsync(
            string prefix,
            FileVisibility visibility,
            CancellationToken cancellationToken = default);
    }

    /// <summary>What to store, and under which regime.</summary>
    /// <param name="Key">
    /// Full key, built by the caller. The name of the uploaded file never becomes a key: it
    /// carries accents, spaces and whatever else the client decides to send.
    /// </param>
    /// <param name="ContentType">Media type, already decided by inspecting the content.</param>
    /// <param name="Visibility">Public or private. This is a business rule, not a bucket setting.</param>
    public sealed record StorageRequest(string Key, string ContentType, FileVisibility Visibility);

    /// <summary>A file that exists in the store.</summary>
    /// <param name="Key">Key to keep in the database. The address is derived from it, never stored.</param>
    /// <param name="ContentType">Media type it was stored with.</param>
    /// <param name="SizeInBytes">Size actually written.</param>
    public sealed record StoredFile(string Key, string ContentType, long SizeInBytes);
}
