using Microsoft.Extensions.Options;
using RevendaPro.Domain.Interfaces.Security;
using RevendaPro.Shared.Exceptions;
using RevendaPro.Shared.Settings;

namespace RevendaPro.Infrastructure.Services.Storage
{
    /// <summary>
    /// Stores photos on disk, in a Docker volume. The database keeps only the file name.
    ///
    /// Swapping to S3 or Azure Blob means implementing IPhotoStorageService again; nothing
    /// above it changes.
    /// </summary>
    public class DiskPhotoStorageService : IPhotoStorageService
    {
        /// <summary>Largest accepted file.</summary>
        public const long MaxSizeInBytes = 2 * 1024 * 1024;

        private static readonly Dictionary<string, string> AllowedTypes =
            new(StringComparer.OrdinalIgnoreCase)
            {
                [".jpg"] = "image/jpeg",
                [".jpeg"] = "image/jpeg",
                [".png"] = "image/png",
                [".webp"] = "image/webp"
            };

        private readonly string _folder;

        /// <summary>Creates the service and makes sure the folder exists.</summary>
        /// <param name="settings">Where the photos are stored.</param>
        public DiskPhotoStorageService(IOptions<RevendaProSettings> settings)
        {
            ArgumentNullException.ThrowIfNull(settings);

            _folder = settings.Value.PhotoFolder;
            Directory.CreateDirectory(_folder);
        }

        /// <inheritdoc/>
        public async Task<string> SaveAsync(
            Stream content,
            string originalFileName,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(content);

            var extension = Path.GetExtension(originalFileName);

            if (!AllowedTypes.ContainsKey(extension))
            {
                throw new BusinessRuleException("Envie uma imagem JPG, PNG ou WEBP.");
            }

            using var memory = new MemoryStream();
            await content.CopyToAsync(memory, cancellationToken).ConfigureAwait(false);

            if (memory.Length == 0)
            {
                throw new BusinessRuleException("O arquivo esta vazio.");
            }

            if (memory.Length > MaxSizeInBytes)
            {
                throw new BusinessRuleException("A imagem passa de 2 MB. Envie um arquivo menor.");
            }

            var bytes = memory.ToArray();

            // The extension can lie, so the file signature is checked before writing.
            if (!SignatureMatches(bytes, extension))
            {
                throw new BusinessRuleException(
                    "O arquivo precisa ser uma imagem JPG, PNG ou WEBP valida.");
            }

            var fileName = $"{Guid.CreateVersion7():N}{extension.ToLowerInvariant()}";

            await File.WriteAllBytesAsync(Path.Combine(_folder, fileName), bytes, cancellationToken)
                .ConfigureAwait(false);

            return fileName;
        }

        /// <inheritdoc/>
        public async Task<StoredPhoto?> ReadAsync(
            string fileName,
            CancellationToken cancellationToken = default)
        {
            var path = SafePath(fileName);

            if (path is null || !File.Exists(path))
            {
                return null;
            }

            var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
            var type = AllowedTypes.GetValueOrDefault(Path.GetExtension(path), "application/octet-stream");

            return new StoredPhoto(new MemoryStream(bytes), type);
        }

        /// <inheritdoc/>
        public Task DeleteAsync(string fileName, CancellationToken cancellationToken = default)
        {
            var path = SafePath(fileName);

            if (path is not null && File.Exists(path))
            {
                File.Delete(path);
            }

            return Task.CompletedTask;
        }

        /// <summary>Stops a name coming from the database escaping the photo folder.</summary>
        private string? SafePath(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName) || fileName != Path.GetFileName(fileName))
            {
                return null;
            }

            var full = Path.GetFullPath(Path.Combine(_folder, fileName));
            var root = Path.GetFullPath(_folder);

            return full.StartsWith(root, StringComparison.Ordinal) ? full : null;
        }

        private static bool SignatureMatches(byte[] bytes, string extension)
        {
            if (bytes.Length < 12)
            {
                return false;
            }

            return extension.ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF,

                ".png" => bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47,

                ".webp" => bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46
                           && bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50,

                _ => false
            };
        }
    }
}
