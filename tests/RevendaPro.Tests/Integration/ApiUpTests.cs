using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace RevendaPro.Tests.Integration
{
    /// <summary>
    /// A API sobe, cria o próprio banco e deixa entrar.
    ///
    /// É o alicerce do M12: enquanto isto não passar, a matriz de perfis e o teste de
    /// isolamento não têm onde acontecer. E ele prova, de graça, o caminho do zero que até
    /// aqui só era conferido à mão — migrations aplicadas, catálogo de telas sincronizado e
    /// primeira revenda semeada.
    /// </summary>
    [Collection(ApiCollection.Name)]
    public class ApiUpTests(ApiFixture api)
    {
        [Fact]
        public async Task TheApiAnswers()
        {
            var answer = await api.Anonymous.GetAsync(new Uri("/health", UriKind.Relative));

            answer.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task TheAdministratorSignsIn_AndReachesEveryScreen()
        {
            var client = await api.AsAdministratorAsync();

            var screens = await ApiFixture.ScreensOfAsync(client);

            // O sincronizador concede cada tela nova ao Administrador, então esta lista é o
            // catálogo inteiro. Ela é a base da matriz do V2.
            screens.Should().Contain(["dashboard", "vehicles", "sales", "market", "users", "roles"]);
        }

        [Theory]
        [InlineData("renata.albuquerque@revendapro.local", "Gestor")]
        [InlineData("sergio.bittencourt@revendapro.local", "Financeiro")]
        [InlineData("joao.vendedor@revendapro.local", "Vendedor")]
        [InlineData("wagner.toledo@revendapro.local", "Oficina")]
        public async Task EveryProfileHasSomebodyToSignInAs(string email, string profile)
        {
            var client = await api.AsAsync(email);

            var screens = await ApiFixture.ScreensOfAsync(client);

            // Sem uma pessoa por perfil, a matriz do V2 teria de inventar gente — e inventar
            // gente é inventar também as telas que ela alcança.
            screens.Should().NotBeEmpty(profile);
            screens.Should().Contain("my-account");
        }

        [Fact]
        public async Task WithoutAToken_TheDoorIsClosed()
        {
            var answer = await api.Anonymous.GetAsync(new Uri("/api/vehicles", UriKind.Relative));

            answer.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task TheWrongPassword_IsRefused()
        {
            var answer = await api.Anonymous.PostAsJsonAsync(
                new Uri("/api/auth/login", UriKind.Relative),
                new { email = "admin@revendapro.local", password = "senha errada" });

            answer.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

            var body = await answer.Content.ReadFromJsonAsync<JsonElement>();

            // A recusa jamais diz qual dos dois estava errado: dizer "essa senha não é dessa
            // conta" confirma que a conta existe.
            body.GetProperty("detail").GetString().Should().Be("E-mail ou senha inválidos.");
        }

        [Fact]
        public async Task TheSeededYard_StartsEmpty_AndTheListingSaysSo()
        {
            var client = await api.AsAdministratorAsync();

            var answer = await client.GetAsync(new Uri("/api/vehicles", UriKind.Relative));

            answer.StatusCode.Should().Be(HttpStatusCode.OK);

            var body = await answer.Content.ReadFromJsonAsync<JsonElement>();

            // Banco recém-criado: a semeadura traz empresa, perfis, telas e gente, e jamais
            // veículo. Um pátio com carro aqui seria dado vazando de outra execução.
            body.GetProperty("data").GetArrayLength().Should().Be(0);
        }
    }
}
