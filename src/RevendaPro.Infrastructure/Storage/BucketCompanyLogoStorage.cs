using RevendaPro.Domain.Enums;
using RevendaPro.Domain.Interfaces.Security;
using RevendaPro.Domain.Interfaces.Storage;
using RevendaPro.Shared.Exceptions;
using RevendaPro.Shared.Helpers;
using SkiaSharp;

namespace RevendaPro.Infrastructure.Storage
{
    /// <summary>
    /// Guarda o logotipo da revenda no bucket privado, em PNG sem perda (M25).
    ///
    /// O que sobe já veio recortado pelo navegador, na proporção do papel. Aqui ele é decodificado
    /// — o que apaga qualquer metadado, porque a re-codificação parte dos pixels —, reduzido a
    /// <see cref="LongestSide"/> pixels quando for maior, e gravado como PNG com o canal de
    /// transparência intacto. Ampliar jamais: inventaria detalhe que nunca existiu.
    ///
    /// A chave é <c>{idTenant}/company/{nome}.png</c>, e só o nome fica na linha. O nome muda a
    /// cada troca, para o navegador jamais continuar mostrando o logotipo antigo.
    /// </summary>
    public class BucketCompanyLogoStorage(IFileStorage storage) : ICompanyLogoStorage
    {
        /// <summary>O maior lado, em pixels. Dois centímetros de papel a 300 dpi são 240; sobra folga.</summary>
        public const int LongestSide = 600;

        /// <summary>Maior envio aceito. Um logotipo maior que isso é uma foto no lugar errado.</summary>
        public const long MaxSizeInBytes = 2 * 1024 * 1024;

        /// <inheritdoc/>
        public async Task<string> SaveAsync(
            int idTenant,
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
                throw new BusinessRuleException("O logotipo passa de 2 MB. Envie um arquivo menor.");
            }

            var bytes = buffer.ToArray();

            // Pelo conteúdo, e jamais pelo nome ou pelo tipo declarado, que o cliente escolhe.
            if (!ImageFormats.IsAccepted(bytes))
            {
                throw new BusinessRuleException("Envie uma imagem JPG, PNG ou WEBP.");
            }

            var png = ToPng(bytes);
            var fileName = $"logo-{Guid.CreateVersion7():N}.png";

            await storage.SaveAsync(
                new MemoryStream(png),
                new StorageRequest(KeyOf(idTenant, fileName), "image/png", FileVisibility.Private),
                cancellationToken).ConfigureAwait(false);

            return fileName;
        }

        /// <inheritdoc/>
        public async Task<StoredPhoto?> ReadAsync(
            int idTenant,
            string fileName,
            CancellationToken cancellationToken = default)
        {
            if (!IsSafe(fileName))
            {
                return null;
            }

            var content = await storage
                .OpenReadAsync(KeyOf(idTenant, fileName), FileVisibility.Private, cancellationToken)
                .ConfigureAwait(false);

            return content is null ? null : new StoredPhoto(content, "image/png");
        }

        /// <inheritdoc/>
        public Task DeleteAsync(int idTenant, string fileName, CancellationToken cancellationToken = default) =>
            IsSafe(fileName)
                ? storage.DeleteAsync(KeyOf(idTenant, fileName), FileVisibility.Private, cancellationToken)
                : Task.CompletedTask;

        /// <summary>
        /// Decodifica, reduz quando preciso e re-codifica em PNG, preservando a transparência.
        /// </summary>
        /// <param name="bytes">O envio.</param>
        /// <returns>O PNG.</returns>
        public static byte[] ToPng(byte[] bytes)
        {
            using var original = SKBitmap.Decode(bytes)
                ?? throw new BusinessRuleException("Envie uma imagem JPG, PNG ou WEBP.");

            var scale = Math.Min(1.0, (double)LongestSide / Math.Max(original.Width, original.Height));

            var width = Math.Max(1, (int)Math.Round(original.Width * scale));
            var height = Math.Max(1, (int)Math.Round(original.Height * scale));

            // O tipo de cor com alfa é pedido explicitamente: um JPG decodifica sem canal alfa, e
            // o PNG que sai dele fica opaco, o que é o certo; um PNG com fundo transparente
            // mantém o fundo transparente, que é o que o papel precisa.
            using var resized = scale < 1.0
                ? original.Resize(
                    new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul),
                    new SKSamplingOptions(SKCubicResampler.Mitchell))
                : null;

            using var image = SKImage.FromBitmap(resized ?? original);
            using var encoded = image.Encode(SKEncodedImageFormat.Png, 100)
                ?? throw new BusinessRuleException("Falha ao processar o logotipo.");

            return encoded.ToArray();
        }

        private static string KeyOf(int idTenant, string fileName) => $"{idTenant}/company/{fileName}";

        /// <summary>Um nome vindo do banco é um segmento, e jamais um caminho.</summary>
        private static bool IsSafe(string fileName) =>
            !string.IsNullOrWhiteSpace(fileName) && !fileName.Contains('/') && !fileName.Contains('\\');
    }
}
