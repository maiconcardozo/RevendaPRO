namespace RevendaPro.Shared.Helpers
{
    /// <summary>
    /// Recognises an image by its first bytes.
    ///
    /// The extension and the Content-Type are chosen by whoever is uploading, so neither
    /// decides anything: an executable renamed to .jpg carries both perfectly. What a file is
    /// can only be read from the file.
    /// </summary>
    public static class ImageFormats
    {
        /// <summary>Media type of the content, or empty when it is something else.</summary>
        /// <param name="content">First bytes of the file. Twelve are enough.</param>
        /// <returns>The media type, or an empty string.</returns>
        public static string Detect(ReadOnlySpan<byte> content)
        {
            if (StartsWith(content, [0xFF, 0xD8, 0xFF]))
            {
                return "image/jpeg";
            }

            if (StartsWith(content, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]))
            {
                return "image/png";
            }

            // RIFF....WEBP: the four bytes between the two markers hold the file length, so
            // they are skipped rather than compared.
            if (StartsWith(content, "RIFF"u8) && content.Length >= 12 &&
                content[8..12].SequenceEqual("WEBP"u8))
            {
                return "image/webp";
            }

            return string.Empty;
        }

        /// <summary>Whether the content is an image this application accepts.</summary>
        /// <param name="content">First bytes of the file.</param>
        /// <returns>True for JPEG, PNG or WebP.</returns>
        public static bool IsAccepted(ReadOnlySpan<byte> content) => Detect(content).Length > 0;

        private static bool StartsWith(ReadOnlySpan<byte> content, ReadOnlySpan<byte> signature) =>
            content.Length >= signature.Length && content[..signature.Length].SequenceEqual(signature);
    }
}
