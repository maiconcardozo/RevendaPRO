using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using FluentAssertions;
using Microsoft.Extensions.Options;
using RevendaPro.Domain.Enums;
using RevendaPro.Infrastructure.Storage;
using RevendaPro.Shared.Settings;

namespace RevendaPro.Tests.Unit
{
    /// <summary>
    /// O endereço que o navegador recebe.
    ///
    /// Nada aqui abre conexão: assinar é cálculo local, e o teste refaz a mesma conta para
    /// conferir o resultado. É assim que se pega o erro que só aparece em runtime — uma URL
    /// bem formada, com assinatura, e mesmo assim recusada com <c>SignatureDoesNotMatch</c>.
    /// </summary>
    public class FileStorageTests
    {
        private const string AccessKey = "revendapro";
        private const string SecretKey = "revendapro-secreta-de-teste";
        private const string Region = "us-east-1";
        private const string Key = "1/vehicles/abc/foto-full.webp";

        [Fact]
        public void ASignedAddress_IsSignedForTheHostTheBrowserCalls()
        {
            // A API fala com minio:9000, nome que só resolve dentro da rede do compose. O
            // navegador fala com localhost:9100. A assinatura da versão 4 cobre o host — a
            // query sai com X-Amz-SignedHeaders=host — então assinar num endereço e reescrever
            // para outro devolve SignatureDoesNotMatch, com a URL parecendo perfeita.
            using var storage = Storage("http://minio:9000", "http://localhost:9100");

            var address = storage.GetUrl(Key, FileVisibility.Private, TimeSpan.FromMinutes(15));

            address.Authority.Should().Be("localhost:9100");

            // O MinIO local só fala http. Uma URL https contra ele quebra no handshake, com
            // um erro de TLS que não diz nada sobre a causa.
            address.Scheme.Should().Be("http");

            SignatureIn(address).Should().Be(
                RecomputedSignature(address),
                "a assinatura tem que fechar com o host que está na própria URL");
        }

        [Fact]
        public void OneEndpoint_SignsForItself()
        {
            // Cloudflare R2 e AWS S3: um endereço só, um cliente só.
            using var storage = Storage("https://conta.r2.cloudflarestorage.com", string.Empty);

            var address = storage.GetUrl(Key, FileVisibility.Private);

            address.Authority.Should().Be("conta.r2.cloudflarestorage.com");
            address.Scheme.Should().Be("https");
            SignatureIn(address).Should().Be(RecomputedSignature(address));
        }

        [Fact]
        public void ASignedAddress_CarriesAnExpiry()
        {
            using var storage = Storage("http://minio:9000", "http://localhost:9100");

            var address = storage.GetUrl(Key, FileVisibility.Private, TimeSpan.FromMinutes(5));

            Query(address)["X-Amz-Expires"].Should().Be("300");
            Query(address)["X-Amz-SignedHeaders"].Should().Be("host");
        }

        [Fact]
        public void APublicAddress_CarriesNoSignature()
        {
            // Um arquivo público assinado seria pior que inútil: o endereço expiraria e o CDN
            // guardaria um link que para de funcionar.
            using var storage = Storage("http://minio:9000", "http://localhost:9100");

            var address = storage.GetUrl(Key, FileVisibility.Public);

            address.Query.Should().BeEmpty();
            address.ToString().Should().Be($"http://localhost:9100/revendapro-public/{Key}");
        }

        private static S3FileStorage Storage(string serviceUrl, string publicUrl) =>
            new(new OptionsWrapper<StorageSettings>(new StorageSettings
            {
                ServiceUrl = serviceUrl,
                PublicUrl = publicUrl,
                Region = Region,
                ForcePathStyle = true,
                AccessKey = AccessKey,
                SecretKey = SecretKey
            }));

        private static System.Collections.Specialized.NameValueCollection Query(Uri address) =>
            HttpUtility.ParseQueryString(address.Query);

        private static string SignatureIn(Uri address) => Query(address)["X-Amz-Signature"]!;

        /// <summary>
        /// Refaz a assinatura da versão 4 a partir da própria URL, do jeito que o servidor de
        /// armazenamento refaz ao receber a requisição.
        /// </summary>
        /// <param name="address">O endereço assinado.</param>
        /// <returns>A assinatura que a URL deveria carregar.</returns>
        private static string RecomputedSignature(Uri address)
        {
            var query = Query(address);

            var canonicalQuery = string.Join(
                '&',
                query.AllKeys
                    .Where(name => name is not null and not "X-Amz-Signature")
                    .OrderBy(name => name, StringComparer.Ordinal)
                    .Select(name => $"{Escape(name!)}={Escape(query[name]!)}"));

            var canonicalRequest = string.Join(
                '\n',
                "GET",
                address.AbsolutePath,
                canonicalQuery,
                $"host:{address.Authority}",
                string.Empty,
                "host",
                "UNSIGNED-PAYLOAD");

            var amzDate = query["X-Amz-Date"]!;
            var scope = query["X-Amz-Credential"]![(AccessKey.Length + 1)..];

            var stringToSign = string.Join(
                '\n',
                "AWS4-HMAC-SHA256",
                amzDate,
                scope,
                Hex(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalRequest))));

            var date = amzDate[..8];

            var signing = Sign(
                Sign(
                    Sign(
                        Sign(Encoding.UTF8.GetBytes($"AWS4{SecretKey}"), date),
                        Region),
                    "s3"),
                "aws4_request");

            return Hex(HMACSHA256.HashData(signing, Encoding.UTF8.GetBytes(stringToSign)));
        }

        private static byte[] Sign(byte[] key, string value) =>
            HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(value));

        private static string Hex(byte[] bytes) =>
            Convert.ToHexString(bytes).ToLower(CultureInfo.InvariantCulture);

        private static string Escape(string value) => Uri.EscapeDataString(value);
    }
}
