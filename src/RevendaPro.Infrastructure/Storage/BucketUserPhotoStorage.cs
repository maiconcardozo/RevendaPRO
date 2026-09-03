using RevendaPro.Domain.Enums;
using RevendaPro.Domain.Interfaces.Security;
using RevendaPro.Domain.Interfaces.Storage;
using RevendaPro.Shared.Exceptions;
using RevendaPro.Shared.Helpers;

namespace RevendaPro.Infrastructure.Storage
{
    /// <summary>
    /// Keeps the photo of a user in the private bucket, through the same port every other file
    /// goes through. The last file of the system to leave the disk.
    ///
    /// The image is reduced to one WebP rendition of the smallest size before it is stored:
    /// an avatar is drawn at forty pixels, and a four megabyte phone picture behind it would
    /// be paid for on every sidebar. The metadata goes with the rest — the same reason the
    /// vehicle photos drop it. See ADR-0004.
    ///
    /// The key is derived from the tenant and the user, and only the file name is kept on the
    /// row: <c>{idTenant}/users/{userCode}/{name}.webp</c>. A file of one company is never
    /// addressable from another (RNF-04).
    /// </summary>
    public class BucketUserPhotoStorage(IFileStorage storage, IImageProcessor images) : IUserPhotoStorage
    {
        /// <summary>Largest accepted upload. An avatar has no business being bigger.</summary>
        public const long MaxSizeInBytes = 2 * 1024 * 1024;

        /// <inheritdoc/>
        public async Task<string> SaveAsync(
            int idTenant,
            Guid userCode,
            Stream content,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(content);

            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);

            if (buffer.Length == 0)
            {
                throw new BusinessRuleException("O arquivo está vazio.");
            }

            if (buffer.Length > MaxSizeInBytes)
            {
                throw new BusinessRuleException("A imagem passa de 2 MB. Envie um arquivo menor.");
            }

            // Judged by the content, never by the name: an executable renamed to .jpg carries
            // the extension perfectly.
            if (!ImageFormats.IsAccepted(buffer.ToArray()))
            {
                throw new BusinessRuleException("Envie uma imagem JPG, PNG ou WEBP.");
            }

            buffer.Position = 0;

            var processed = await images.ProcessAsync(buffer, cancellationToken).ConfigureAwait(false);

            var thumbnail = processed.Renditions.Single(r => r.Size == ImageSize.Thumbnail);
            var fileName = $"{Guid.CreateVersion7():N}.webp";

            await storage.SaveAsync(
                new MemoryStream(thumbnail.Content),
                new StorageRequest(KeyOf(idTenant, userCode, fileName), "image/webp", FileVisibility.Private),
                cancellationToken).ConfigureAwait(false);

            return fileName;
        }

        /// <inheritdoc/>
        public async Task<StoredPhoto?> ReadAsync(
            int idTenant,
            Guid userCode,
            string fileName,
            CancellationToken cancellationToken = default)
        {
            if (!IsSafe(fileName))
            {
                return null;
            }

            var content = await storage
                .OpenReadAsync(KeyOf(idTenant, userCode, fileName), FileVisibility.Private, cancellationToken)
                .ConfigureAwait(false);

            return content is null ? null : new StoredPhoto(content, "image/webp");
        }

        /// <inheritdoc/>
        public Task DeleteAsync(
            int idTenant,
            Guid userCode,
            string fileName,
            CancellationToken cancellationToken = default) =>
            IsSafe(fileName)
                ? storage.DeleteAsync(KeyOf(idTenant, userCode, fileName), FileVisibility.Private, cancellationToken)
                : Task.CompletedTask;

        private static string KeyOf(int idTenant, Guid userCode, string fileName) =>
            $"{idTenant}/users/{userCode}/{fileName}";

        /// <summary>A name from the database is one segment, never a path.</summary>
        private static bool IsSafe(string fileName) =>
            !string.IsNullOrWhiteSpace(fileName) && !fileName.Contains('/') && !fileName.Contains('\\');
    }
}
