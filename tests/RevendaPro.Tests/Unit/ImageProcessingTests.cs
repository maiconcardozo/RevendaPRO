using System.Text;
using FluentAssertions;
using RevendaPro.Domain.Enums;
using RevendaPro.Infrastructure.Storage;
using RevendaPro.Shared.Exceptions;
using RevendaPro.Shared.Helpers;
using SkiaSharp;

namespace RevendaPro.Tests.Unit
{
    /// <summary>
    /// What has to be true of every image before it is allowed to exist: it is really an
    /// image, it carries no metadata, and it comes in the three sizes the screen asks for.
    /// </summary>
    public class ImageProcessingTests
    {
        private readonly SkiaImageProcessor _processor = new();

        [Fact]
        public void Detection_ReadsTheContentAndNeverTheName()
        {
            ImageFormats.Detect([0xFF, 0xD8, 0xFF, 0xE0]).Should().Be("image/jpeg");
            ImageFormats.Detect([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]).Should().Be("image/png");
            ImageFormats.Detect(Encoding.ASCII.GetBytes("RIFF____WEBPVP8 ")).Should().Be("image/webp");
        }

        [Fact]
        public void Detection_RefusesWhatIsNotAnImage()
        {
            // A Windows executable, which is what arrives when somebody renames a file.
            ImageFormats.IsAccepted(Encoding.ASCII.GetBytes("MZ\0")).Should().BeFalse();

            // RIFF without WEBP is a WAV, and shares the first four bytes with a WebP.
            ImageFormats.IsAccepted(Encoding.ASCII.GetBytes("RIFF____WAVEfmt ")).Should().BeFalse();

            ImageFormats.IsAccepted([]).Should().BeFalse();
        }

        [Fact]
        public async Task AnExecutableRenamedToJpg_IsRefused()
        {
            var content = new MemoryStream(Encoding.ASCII.GetBytes("MZ\0 this is not a photo"));

            var act = () => _processor.ProcessAsync(content);

            await act.Should().ThrowAsync<BusinessRuleException>();
        }

        [Fact]
        public async Task AnEmptyFile_IsRefused()
        {
            var act = () => _processor.ProcessAsync(new MemoryStream());

            await act.Should().ThrowAsync<BusinessRuleException>();
        }

        [Fact]
        public async Task EveryPhoto_ComesOutInThreeWebpSizes()
        {
            var photo = JpegOf(2400, 1600);

            var processed = await _processor.ProcessAsync(new MemoryStream(photo));

            processed.Renditions.Should().HaveCount(3);
            processed.Renditions.Select(r => r.Size).Should().BeEquivalentTo(
                [ImageSize.Thumbnail, ImageSize.Card, ImageSize.Full]);

            foreach (var rendition in processed.Renditions)
            {
                ImageFormats.Detect(rendition.Content).Should().Be("image/webp");

                Math.Max(rendition.Width, rendition.Height)
                    .Should().Be((int)rendition.Size, "the longest side is what the size names");
            }

            // The aspect ratio survives: 2400x1600 is 3:2, and so is every rendition.
            foreach (var rendition in processed.Renditions)
            {
                ((double)rendition.Width / rendition.Height).Should().BeApproximately(1.5, 0.01);
            }
        }

        [Fact]
        public async Task ASmallPhoto_IsNeverEnlarged()
        {
            // Enlarging invents detail that was never there, and costs bytes to do it.
            var photo = JpegOf(240, 160);

            var processed = await _processor.ProcessAsync(new MemoryStream(photo));

            foreach (var rendition in processed.Renditions)
            {
                rendition.Width.Should().BeLessThanOrEqualTo(240);
                rendition.Height.Should().BeLessThanOrEqualTo(160);
            }
        }

        [Fact]
        public async Task TheProcessedPhoto_CarriesNoMetadata()
        {
            // A photo from a phone carries GPS coordinates. Published as it arrived, the
            // advertisement reveals where the picture was taken.
            var photo = JpegWithExif(1200, 800);

            Contains(photo, "Exif\0\0"u8).Should().BeTrue("the input has to carry EXIF for this to prove anything");

            var processed = await _processor.ProcessAsync(new MemoryStream(photo));

            foreach (var rendition in processed.Renditions)
            {
                Contains(rendition.Content, "Exif\0\0"u8).Should().BeFalse();
                Contains(rendition.Content, "GPSLatitude"u8).Should().BeFalse();
            }
        }

        [Fact]
        public async Task TheRenditions_AreSmallerThanTheOriginal()
        {
            var photo = JpegOf(2400, 1600);

            var processed = await _processor.ProcessAsync(new MemoryStream(photo));

            processed.TotalSizeInBytes.Should().BeLessThan(photo.Length,
                "serving three WebP renditions has to cost less than the single original");
        }

        /// <summary>A photograph-like image, so the encoder has real detail to work with.</summary>
        private static byte[] JpegOf(int width, int height)
        {
            using var bitmap = new SKBitmap(width, height);
            using var canvas = new SKCanvas(bitmap);

            canvas.Clear(SKColors.SlateGray);

            using var paint = new SKPaint { IsAntialias = true };
            var random = new Random(7);

            for (var i = 0; i < 400; i++)
            {
                paint.Color = new SKColor(
                    (byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256));

                canvas.DrawCircle(
                    random.Next(width), random.Next(height), random.Next(10, 90), paint);
            }

            using var image = SKImage.FromBitmap(bitmap);
            using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, 92);

            return encoded.ToArray();
        }

        /// <summary>
        /// The same image with an EXIF block inserted right after the start marker, which is
        /// where a camera puts it. Built by hand because Skia writes no metadata.
        /// </summary>
        private static byte[] JpegWithExif(int width, int height)
        {
            var jpeg = JpegOf(width, height);

            var payload = new List<byte>();
            payload.AddRange("Exif\0\0"u8.ToArray());
            payload.AddRange("MM\0*\0\0\0"u8.ToArray());
            payload.AddRange("GPSLatitude"u8.ToArray());
            payload.AddRange(Encoding.ASCII.GetBytes("-23.5505,-46.6333"));

            var length = payload.Count + 2;

            var result = new List<byte>();
            result.AddRange(jpeg[..2]);                              // SOI
            result.AddRange([0xFF, 0xE1]);                           // APP1
            result.AddRange([(byte)(length >> 8), (byte)(length & 0xFF)]);
            result.AddRange(payload);
            result.AddRange(jpeg[2..]);

            return [.. result];
        }

        private static bool Contains(ReadOnlySpan<byte> content, ReadOnlySpan<byte> marker)
        {
            for (var i = 0; i + marker.Length <= content.Length; i++)
            {
                if (content.Slice(i, marker.Length).SequenceEqual(marker))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
