using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using SkiaSharp;

namespace RevendaPro.Tests.Integration
{
    /// <summary>
    /// O logotipo da revenda (M25), com a API no ar.
    ///
    /// O que se prova: subir vira PNG e passa a existir; trocar muda a versão e o antigo some;
    /// remover devolve o 404; quem manda coisa que não é imagem é recusado; e a outra revenda
    /// enxerga logotipo nenhum — o arquivo é dela, e a chave começa pelo tenant.
    /// </summary>
    [Collection(ApiCollection.Name)]
    public class CompanyLogoTests(ApiFixture api) : IAsyncLifetime
    {
        private HttpClient boss = default!;
        private SecondDealership other = default!;

        /// <inheritdoc/>
        public async Task InitializeAsync()
        {
            boss = await api.ClientOfAsync("admin@revendapro.local");
            other = await api.OtherDealershipAsync();
        }

        /// <inheritdoc/>
        public async Task DisposeAsync()
        {
            // A revenda volta a ficar sem logotipo, como os outros testes a encontram.
            await boss.DeleteAsync(Url("/api/company/logo"));
        }

        [Fact]
        public async Task SubirOLogotipo_OTornaVisivel_ComoPng_EComVersao()
        {
            var saved = await ReadDataAsync(await boss.PostAsync(Url("/api/company/logo"), Png(320, 160, SKColors.Red)));

            saved.GetProperty("hasLogo").GetBoolean().Should().BeTrue();
            saved.GetProperty("logoVersion").GetString().Should().StartWith("logo-").And.EndWith(".png");

            var read = await boss.GetAsync(Url("/api/company/logo"));

            read.StatusCode.Should().Be(HttpStatusCode.OK);
            read.Content.Headers.ContentType!.MediaType.Should().Be("image/png");

            var bytes = await read.Content.ReadAsByteArrayAsync();

            // A assinatura do PNG: o logotipo sai PNG sem perda, e jamais o WebP da galeria.
            bytes[..8].Should().Equal(0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A);

            var company = await ReadDataAsync(await boss.GetAsync(Url("/api/company")));
            company.GetProperty("hasLogo").GetBoolean().Should().BeTrue();
        }

        [Fact]
        public async Task UmJpgVira_Png_EUmaImagemGrande_EReduzida()
        {
            await ReadDataAsync(await boss.PostAsync(Url("/api/company/logo"), Jpeg(2400, 1200)));

            var bytes = await (await boss.GetAsync(Url("/api/company/logo"))).Content.ReadAsByteArrayAsync();

            using var decoded = SKBitmap.Decode(bytes);

            decoded.Width.Should().Be(600, "dois centímetros de papel a 300 dpi são 240 pixels; 600 sobra");
            decoded.Height.Should().Be(300);
        }

        [Fact]
        public async Task TrocarOLogotipo_MudaAVersao()
        {
            var first = await ReadDataAsync(await boss.PostAsync(Url("/api/company/logo"), Png(200, 100, SKColors.Blue)));
            var second = await ReadDataAsync(await boss.PostAsync(Url("/api/company/logo"), Png(200, 100, SKColors.Green)));

            second.GetProperty("logoVersion").GetString().Should().NotBe(
                first.GetProperty("logoVersion").GetString(),
                "um nome fixo faria o navegador continuar mostrando o logotipo antigo");
        }

        [Fact]
        public async Task RemoverOLogotipo_DevolveO404_EOsDadosDizemQueNaoHa()
        {
            await ReadDataAsync(await boss.PostAsync(Url("/api/company/logo"), Png(200, 100, SKColors.Black)));

            var removed = await ReadDataAsync(await boss.DeleteAsync(Url("/api/company/logo")));

            removed.GetProperty("hasLogo").GetBoolean().Should().BeFalse();
            removed.GetProperty("logoVersion").ValueKind.Should().Be(JsonValueKind.Null);

            (await boss.GetAsync(Url("/api/company/logo"))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task OQueNaoEImagem_ERecusado_PeloConteudo()
        {
            var content = new MultipartFormDataContent();
            var file = new ByteArrayContent("isto é um texto com extensão de imagem"u8.ToArray());
            file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            content.Add(file, "file", "logo.png");

            var answer = await boss.PostAsync(Url("/api/company/logo"), content);

            // Pelo conteúdo, e jamais pelo nome ou pelo tipo declarado, que o cliente escolhe.
            answer.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        }

        [Fact]
        public async Task AOutraRevenda_EnxergaLogotipoNenhum()
        {
            await ReadDataAsync(await boss.PostAsync(Url("/api/company/logo"), Png(200, 100, SKColors.Purple)));

            var hers = await api.AsAsync(other.AdminEmail);

            (await hers.GetAsync(Url("/api/company/logo"))).StatusCode.Should().Be(
                HttpStatusCode.NotFound, "o logotipo é da revenda, e a chave começa pelo tenant (RNF-04)");
        }

        private static MultipartFormDataContent Png(int width, int height, SKColor color)
        {
            using var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.Transparent);
            canvas.DrawCircle(width / 2f, height / 2f, height / 3f, new SKPaint { Color = color });

            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);

            return Upload(data.ToArray(), "image/png", "logo.png");
        }

        private static MultipartFormDataContent Jpeg(int width, int height)
        {
            using var bitmap = new SKBitmap(width, height);
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.Orange);

            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Jpeg, 90);

            return Upload(data.ToArray(), "image/jpeg", "logo.jpg");
        }

        private static MultipartFormDataContent Upload(byte[] bytes, string contentType, string name)
        {
            var content = new MultipartFormDataContent();
            var file = new ByteArrayContent(bytes);
            file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            content.Add(file, "file", name);

            return content;
        }

        private static async Task<JsonElement> ReadDataAsync(HttpResponseMessage answer)
        {
            answer.IsSuccessStatusCode.Should().BeTrue(await answer.Content.ReadAsStringAsync());

            var body = await answer.Content.ReadFromJsonAsync<JsonElement>();

            return body.GetProperty("data");
        }

        private static Uri Url(string path) => new(path, UriKind.Relative);
    }
}
