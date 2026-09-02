using RevendaPro.Domain.Enums;

namespace RevendaPro.Domain.Interfaces.Storage
{
    /// <summary>
    /// Turns what somebody uploaded into what the site serves.
    ///
    /// Three things have to happen before an image is allowed to exist, and this port is where
    /// the domain states them:
    ///
    /// <list type="number">
    /// <item>refuse anything that is not an accepted image, judging by the content and never
    /// by the extension or the declared media type, both of which the client chooses;</item>
    /// <item>drop the metadata, because a photo from a phone carries GPS coordinates — the
    /// original published in an advertisement reveals where it was taken;</item>
    /// <item>produce the renditions, so the screen asks for the smallest one that fits instead
    /// of pulling four megabytes to fill a small square.</item>
    /// </list>
    ///
    /// See ADR-0004.
    /// </summary>
    public interface IImageProcessor
    {
        /// <summary>Reads the content and answers the renditions to store.</summary>
        /// <param name="content">The uploaded bytes.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The renditions, plus the dimensions of the largest one.</returns>
        /// <exception cref="RevendaPro.Shared.Exceptions.BusinessRuleException">
        /// When the content is empty, larger than allowed, or something other than an accepted
        /// image.
        /// </exception>
        Task<ProcessedImage> ProcessAsync(Stream content, CancellationToken cancellationToken = default);
    }

    /// <summary>An image ready to be stored.</summary>
    /// <param name="Renditions">One entry per <see cref="ImageSize"/>.</param>
    /// <param name="Width">Width of the largest rendition.</param>
    /// <param name="Height">Height of the largest rendition.</param>
    public sealed record ProcessedImage(
        IReadOnlyList<ImageRendition> Renditions,
        int Width,
        int Height)
    {
        /// <summary>Bytes of every rendition together, which is what the gallery costs to keep.</summary>
        public long TotalSizeInBytes => Renditions.Sum(r => (long)r.Content.Length);
    }

    /// <summary>One size of one image.</summary>
    /// <param name="Size">Which rendition this is.</param>
    /// <param name="Content">Encoded bytes, always WebP.</param>
    /// <param name="Width">Width in pixels.</param>
    /// <param name="Height">Height in pixels.</param>
    public sealed record ImageRendition(ImageSize Size, byte[] Content, int Width, int Height);
}
