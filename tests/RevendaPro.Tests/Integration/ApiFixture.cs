using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RevendaPro.Domain.Interfaces.Reference;
using RevendaPro.Domain.Interfaces.Storage;
using Testcontainers.MariaDb;

namespace RevendaPro.Tests.Integration
{
    /// <summary>
    /// A API de verdade, no ar, contra um banco de verdade.
    ///
    /// <b>O banco vive num contêiner descartável, e não em memória.</b> O acesso a dado deste
    /// sistema é Dapper com SQL escrito à mão (ADR-0003) — crase em palavra reservada,
    /// <c>INTERVAL</c>, <c>DATEDIFF</c>. Um banco em memória responderia a um SQL que não é o
    /// nosso, e o teste passaria enquanto a produção quebra, que é pior do que não ter teste.
    ///
    /// Depender da pilha de desenvolvimento também está fora: <c>dotnet test</c> precisa
    /// continuar sendo <b>um comando</b>, e não um procedimento. O contêiner sobe com a suíte e
    /// morre com ela.
    ///
    /// Fonte externa nenhuma é tocada: a tabela FIPE e o armazenamento entram como dublês, e a
    /// rotina mensal do pátio fica desligada pelo interruptor que já existe. Ver
    /// <c>docs/plans/m12-matriz-perfil-endpoint.md</c>.
    /// </summary>
    public sealed class ApiFixture : IAsyncLifetime
    {
        /// <summary>A senha de todo mundo nesta pilha de teste. Ela morre com o contêiner.</summary>
        private const string Password = "Teste@123456";

        private const string AdminEmail = "admin@revendapro.local";

        private readonly MariaDbContainer database = new MariaDbBuilder()
            // A mesma imagem da pilha de desenvolvimento e da de produção: um teste que roda
            // contra outra versão do banco prova outra coisa.
            .WithImage("mariadb:11.8")
            .WithDatabase("revendapro")
            .WithUsername("root")
            .WithPassword(Password)
            .Build();

        private readonly Dictionary<string, HttpClient> signedIn = new(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, IReadOnlyList<string>> reached =
            new(StringComparer.OrdinalIgnoreCase);

        private WebApplicationFactory<Program>? api;

        /// <summary>Um cliente sem token, para o que precisa ser recusado.</summary>
        public HttpClient Anonymous { get; private set; } = default!;

        /// <inheritdoc/>
        public async Task InitializeAsync()
        {
            await database.StartAsync().ConfigureAwait(false);

            Configure();

            api = new Factory();
            Anonymous = api.CreateClient();

            // A primeira chamada é o que de fato sobe a API: as migrations, o catálogo de
            // telas e a semeadura acontecem no arranque.
            await Anonymous.GetAsync(new Uri("/health", UriKind.Relative)).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task DisposeAsync()
        {
            Anonymous?.Dispose();

            if (api is not null)
            {
                await api.DisposeAsync().ConfigureAwait(false);
            }

            await database.DisposeAsync().ConfigureAwait(false);
        }

        /// <summary>Um cliente autenticado como o administrador da revenda piloto.</summary>
        /// <returns>O cliente, com o token no cabeçalho.</returns>
        public Task<HttpClient> AsAdministratorAsync() => AsAsync(AdminEmail);

        /// <summary>
        /// Um cliente autenticado como a pessoa que ocupa um perfil.
        ///
        /// Os usuários de demonstração nascem com o sistema, um por perfil, e é isso que
        /// permite entrar como cada um deles sem inventar gente.
        /// </summary>
        /// <param name="email">E-mail de quem entra.</param>
        /// <returns>O cliente, com o token no cabeçalho.</returns>
        public async Task<HttpClient> AsAsync(string email)
        {
            var client = api!.CreateClient();

            var answer = await client.PostAsJsonAsync(
                new Uri("/api/auth/login", UriKind.Relative),
                new { email, password = Password })
                .ConfigureAwait(false);

            answer.EnsureSuccessStatusCode();

            var body = await answer.Content.ReadFromJsonAsync<JsonElement>().ConfigureAwait(false);

            var token = body
                .GetProperty("data")
                .GetProperty("tokens")
                .GetProperty("accessToken")
                .GetString();

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            return client;
        }

        /// <summary>
        /// O cliente de quem já entrou, reaproveitado.
        ///
        /// A matriz do V2 faz centenas de chamadas por perfil; entrar de novo a cada uma delas
        /// gastaria a suíte inteira provando o login, que já tem teste próprio.
        /// </summary>
        /// <param name="email">E-mail de quem entra.</param>
        /// <returns>O cliente, com o token no cabeçalho.</returns>
        public async Task<HttpClient> ClientOfAsync(string email)
        {
            if (signedIn.TryGetValue(email, out var known))
            {
                return known;
            }

            var client = await AsAsync(email).ConfigureAwait(false);
            signedIn[email] = client;

            return client;
        }

        /// <summary>As telas de um perfil, ditas pelo próprio sistema e guardadas.</summary>
        /// <param name="email">E-mail de quem ocupa o perfil.</param>
        /// <returns>As chaves de tela.</returns>
        public async Task<IReadOnlyList<string>> ScreensOfAsync(string email)
        {
            if (reached.TryGetValue(email, out var known))
            {
                return known;
            }

            var client = await ClientOfAsync(email).ConfigureAwait(false);
            var screens = await ScreensOfAsync(client).ConfigureAwait(false);

            reached[email] = screens;

            return screens;
        }

        /// <summary>As telas que uma sessão alcança, ditas pelo próprio sistema.</summary>
        /// <param name="client">Cliente já autenticado.</param>
        /// <returns>As chaves de tela.</returns>
        public static async Task<IReadOnlyList<string>> ScreensOfAsync(HttpClient client)
        {
            ArgumentNullException.ThrowIfNull(client);

            var answer = await client.GetAsync(new Uri("/api/auth/me", UriKind.Relative))
                .ConfigureAwait(false);

            answer.EnsureSuccessStatusCode();

            var body = await answer.Content.ReadFromJsonAsync<JsonElement>().ConfigureAwait(false);

            return [.. body.GetProperty("data").GetProperty("screens")
                .EnumerateArray()
                .Select(screen => screen.GetString() ?? string.Empty)];
        }

        /// <summary>Um escopo de serviços da API no ar, para montar cenário pelo próprio sistema.</summary>
        /// <returns>O escopo. Quem pede, descarta.</returns>
        public IServiceScope Scope() => api!.Services.CreateScope();

        /// <summary>
        /// A configuração da API, por variável de ambiente.
        ///
        /// <b>Não é preguiça: é o único ponto em que ela chega a tempo.</b> Um arquivo de
        /// instruções de topo lê <c>builder.Configuration</c> antes de a montagem existir — a
        /// chave do JWT e a cadeia de conexão são conferidas ali, na linha 24 do
        /// <c>Program</c>. O que a fábrica de teste acrescenta depois só aparece no
        /// <c>Build()</c>, e portanto tarde demais.
        ///
        /// Vale para o processo do teste inteiro, que é exatamente o alcance desejado.
        /// </summary>
        private void Configure()
        {
            var settings = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["RevendaPro__ConnectionString"] =
                    $"server={database.Hostname};port={database.GetMappedPublicPort(3306)};"
                    + $"database=revendapro;user=root;password={Password};",

                ["RevendaPro__PilotTenant"] = "Revenda Piloto",
                ["RevendaPro__AdminEmail"] = AdminEmail,
                ["RevendaPro__AdminPassword"] = Password,

                // A tripulação de demonstração nasce com o sistema, uma pessoa por perfil. É
                // ela que permite entrar como cada perfil na matriz do V2.
                ["RevendaPro__SeedDemoUsers"] = "true",
                ["RevendaPro__DemoPassword"] = Password,

                ["Jwt__Key"] = new string('t', 64),

                // Sem criar bucket e sem versionamento: o armazenamento é dublê, e o arranque
                // jamais tenta alcançar um MinIO que não existe aqui.
                ["Storage__CreateBucketsOnStartup"] = "false",
                ["Storage__KeepFileVersions"] = "false",

                // A rotina do pátio acordando no meio da suíte tornaria o resultado dependente
                // do relógio. O interruptor já existe desde o M11.
                ["Fipe__RefreshYard"] = "false",
                ["Fipe__Enabled"] = "false",
            };

            foreach (var (key, value) in settings)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }

        /// <summary>
        /// A montagem da API, com o que precisa mudar para caber num teste — e nada além.
        /// </summary>
        private sealed class Factory : WebApplicationFactory<Program>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                ArgumentNullException.ThrowIfNull(builder);

                builder.UseEnvironment("Testing");

                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton<IFipeCatalog, FipeCatalogDouble>();
                    services.AddSingleton<IFileStorage, FileStorageDouble>();
                });
            }
        }
    }

    /// <summary>
    /// Compartilha uma API e um banco por execução da suíte.
    ///
    /// Subir um contêiner por classe de teste multiplicaria por dez o tempo da suíte para
    /// provar exatamente a mesma coisa.
    /// </summary>
    [CollectionDefinition(Name)]
    public sealed class ApiCollection : ICollectionFixture<ApiFixture>
    {
        public const string Name = "API no ar";
    }
}
