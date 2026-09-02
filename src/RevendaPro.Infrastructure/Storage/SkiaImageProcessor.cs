using RevendaPro.Domain.Enums;
using RevendaPro.Domain.Interfaces.Storage;
using RevendaPro.Shared.Exceptions;
using RevendaPro.Shared.Helpers;
using SkiaSharp;

namespace RevendaPro.Infrastructure.Storage
{
    /// <summary>
    /// Processes images with SkiaSharp.
    ///
    /// SkiaSharp is MIT. ImageSharp, the more obvious choice, moved to a commercial licence
    /// above a revenue threshold — the same trap already recorded for FluentAssertions and
    /// MediatR in PADRAO-GLOBAL.md.
    /// </summary>
    public sealed class SkiaImageProcessor : IImageProcessor
    {
        /// <summary>Largest accepted upload.</summary>
        public const long MaxSizeInBytes = 12 * 1024 * 1024;

        /// <summary>
        /// Encoding quality. Eighty is where WebP stops being distinguishable from the
        /// original to the eye while staying far smaller than the JPEG it came from.
        /// </summary>
        private const int Quality = 80;

        /// <inheritdoc/>
        public async Task<ProcessedImage> ProcessAsync(
            Stream content,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(content);

            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);

            if (buffer.Length == 0)
            {
                throw new BusinessRuleException("Envie um arquivo com conteúdo.");
            }

            if (buffer.Length > MaxSizeInBytes)
            {
                throw new BusinessRuleException(
                    $"Envie uma imagem de até {MaxSizeInBytes / (1024 * 1024)} MB.");
            }

            var bytes = buffer.ToArray();

            // Judged by the content, and never by the extension or the declared media type.
            if (!ImageFormats.IsAccepted(bytes))
            {
                throw new BusinessRuleException("Envie uma imagem JPG, PNG ou WEBP.");
            }

            using var original = Decode(bytes);

            var renditions = Enum.GetValues<ImageSize>()
                .Select(size => Render(original, size))
                .ToList();

            return new ProcessedImage(renditions, original.Width, original.Height);
        }

        /// <summary>
        /// Decodes and puts the picture upright.
        ///
        /// A phone stores the frame as the sensor read it and records the rotation as EXIF
        /// orientation. Dropping the metadata without applying it first — which is what a
        /// naive decode plus re-encode does — leaves every portrait photo lying on its side.
        /// The rotation has to be baked into the pixels while the metadata still exists.
        /// </summary>
        private static SKBitmap Decode(byte[] bytes)
        {
            using var data = SKData.CreateCopy(bytes);
            using var codec = SKCodec.Create(data)
                ?? throw new BusinessRuleException("Envie uma imagem JPG, PNG ou WEBP.");

            var bitmap = SKBitmap.Decode(codec)
                ?? throw new BusinessRuleException("Envie uma imagem JPG, PNG ou WEBP.");

            var upright = Straighten(bitmap, codec.EncodedOrigin);

            if (!ReferenceEquals(upright, bitmap))
            {
                bitmap.Dispose();
            }

            return upright;
        }

        private static SKBitmap Straighten(SKBitmap bitmap, SKEncodedOrigin origin)
        {
            if (origin == SKEncodedOrigin.TopLeft || origin == SKEncodedOrigin.Default)
            {
                return bitmap;
            }

            // A quarter turn swaps the sides; a flip or a half turn keeps them.
            var turned = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop
                or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;

            var width = turned ? bitmap.Height : bitmap.Width;
            var height = turned ? bitmap.Width : bitmap.Height;

            var result = new SKBitmap(width, height, bitmap.ColorType, bitmap.AlphaType);

            using var canvas = new SKCanvas(result);

            switch (origin)
            {
                case SKEncodedOrigin.TopRight:
                    canvas.Scale(-1, 1);
                    canvas.Translate(-width, 0);
                    break;
                case SKEncodedOrigin.BottomRight:
                    canvas.RotateDegrees(180, width / 2f, height / 2f);
                    break;
                case SKEncodedOrigin.BottomLeft:
                    canvas.Scale(1, -1);
                    canvas.Translate(0, -height);
                    break;
                case SKEncodedOrigin.LeftTop:
                    canvas.RotateDegrees(90);
                    canvas.Scale(1, -1);
                    break;
                case SKEncodedOrigin.RightTop:
                    canvas.Translate(width, 0);
                    canvas.RotateDegrees(90);
                    break;
                case SKEncodedOrigin.RightBottom:
                    canvas.Translate(width, 0);
                    canvas.RotateDegrees(90);
                    canvas.Scale(1, -1);
                    canvas.Translate(0, -height);
                    break;
                case SKEncodedOrigin.LeftBottom:
                    canvas.Translate(0, height);
                    canvas.RotateDegrees(-90);
                    break;
                default:
                    break;
            }

            canvas.DrawBitmap(bitmap, 0, 0);

            return result;
        }

        /// <summary>
        /// Encodes one rendition. Re-encoding from raw pixels is what removes the metadata:
        /// what comes out carries no EXIF, because there is nowhere for it to come from.
        /// </summary>
        private static ImageRendition Render(SKBitmap original, ImageSize size)
        {
            var longest = (int)size;
            var scale = Math.Min(
                1.0,
                (double)longest / Math.Max(original.Width, original.Height));

            var width = Math.Max(1, (int)Math.Round(original.Width * scale));
            var height = Math.Max(1, (int)Math.Round(original.Height * scale));

            // Enlarging invents detail that was never there, and costs bytes to do it.
            using var resized = scale < 1.0
                ? original.Resize(new SKImageInfo(width, height), new SKSamplingOptions(SKCubicResampler.Mitchell))
                : null;

            using var image = SKImage.FromBitmap(resized ?? original);
            using var encoded = image.Encode(SKEncodedImageFormat.Webp, Quality)
                ?? throw new BusinessRuleException("Falha ao processar a imagem enviada.");

            return new ImageRendition(size, encoded.ToArray(), width, height);
        }
    }
}
