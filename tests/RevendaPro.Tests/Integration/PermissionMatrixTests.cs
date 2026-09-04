using System.Net;
using System.Net.Http.Headers;
using System.Text;
using FluentAssertions;

namespace RevendaPro.Tests.Integration
{
    /// <summary>
    /// A matriz perfil × endpoint, com a API no ar.
    ///
    /// O <c>ApiGuardTests</c> prova que a fechadura está <b>instalada</b>: todo endpoint declara
    /// a tela que o protege, inclusive os criados amanhã. Ele jamais prova que ela
    /// <b>tranca</b> — e é isso que acontece aqui, chamando cada endereço com cada perfil.
    ///
    /// <b>Cada célula responde uma pergunta só: 403 ou passou?</b> Quando o perfil tem a tela, a
    /// resposta pode ser 200, 400, 404 ou 422 — todas significam a mesma coisa aqui: a
    /// autorização deixou passar. Exigir 200 obrigaria cada célula a montar um corpo válido e um
    /// id existente, e transformaria um teste de segurança em 63 testes de negócio mal escritos.
    ///
    /// Como os identificadores são aleatórios e os corpos vazios, a matriz bate na fechadura sem
    /// entrar na casa: ela jamais cria nem destrói uma linha.
    /// </summary>
    [Collection(ApiCollection.Name)]
    public class PermissionMatrixTests(ApiFixture api)
    {
        /// <summary>
        /// Uma pessoa por perfil, como o sistema as semeia.
        ///
        /// As telas de cada uma <b>não</b> aparecem aqui: elas são perguntadas ao próprio
        /// sistema. Uma tabela de permissões escrita à mão no teste envelhece no primeiro
        /// endpoint novo, e envelhece em silêncio.
        /// </summary>
        private static readonly (string Profile, string Email)[] Crew =
        [
            ("Administrador", "admin@revendapro.local"),
            ("Gestor", "renata.albuquerque@revendapro.local"),
            ("Financeiro", "sergio.bittencourt@revendapro.local"),
            ("Vendedor", "joao.vendedor@revendapro.local"),
            ("Oficina", "wagner.toledo@revendapro.local"),
        ];

        public static TheoryData<string, string, string, string, string?> Guarded()
        {
            var data = new TheoryData<string, string, string, string, string?>();

            foreach (var endpoint in ApiEndpoints.All.Where(e => !e.Anonymous && e.Screen is not null))
            {
                data.Add(endpoint.Name, endpoint.Method, endpoint.Url, endpoint.Screen!, endpoint.Consumes);
            }

            return data;
        }

        public static TheoryData<string, string, string, string?> Authenticated()
        {
            var data = new TheoryData<string, string, string, string?>();

            foreach (var endpoint in ApiEndpoints.All.Where(e => !e.Anonymous))
            {
                data.Add(endpoint.Name, endpoint.Method, endpoint.Url, endpoint.Consumes);
            }

            return data;
        }

        [Theory]
        [MemberData(nameof(Guarded))]
        public async Task TheEndpointAnswersAccordingToTheScreensOfEachProfile(
            string name,
            string method,
            string url,
            string screen,
            string? consumes)
        {
            var wrong = new List<string>();

            foreach (var (profile, email) in Crew)
            {
                var screens = await api.ScreensOfAsync(email);
                var client = await api.ClientOfAsync(email);

                var answer = await Call(client, method, url, consumes);
                var refused = answer.StatusCode == HttpStatusCode.Forbidden;

                // A expectativa é derivada, e jamais escrita: ela sai das telas que o próprio
                // sistema diz que este perfil alcança.
                var shouldPass = screens.Contains(screen);

                if (refused == shouldPass)
                {
                    wrong.Add(
                        $"{profile} {(shouldPass ? "deveria passar" : "deveria levar 403")} "
                        + $"e recebeu {(int)answer.StatusCode}");
                }
            }

            wrong.Should().BeEmpty($"{name} exige a tela \"{screen}\" ({method} {url})");
        }

        [Theory]
        [MemberData(nameof(Authenticated))]
        public async Task WithoutAToken_TheEndpointAnswers401(
            string name,
            string method,
            string url,
            string? consumes)
        {
            var answer = await Call(api.Anonymous, method, url, consumes);

            // 401 e jamais 403: sem token não há perfil a julgar, e a resposta certa é "quem é
            // você?", que é o que faz o frontend mandar para a tela de entrada.
            answer.StatusCode.Should().Be(
                HttpStatusCode.Unauthorized, "{0} ({1} {2}) exige sessão", name, method, url);
        }

        [Fact]
        public async Task AnEndpointThatAsksOnlyForASession_LetsEveryProfileThrough()
        {
            var sessionOnly = ApiEndpoints.All
                .Where(endpoint => !endpoint.Anonymous && endpoint.Screen is null)
                .ToList();

            sessionOnly.Should().NotBeEmpty("a sessão do usuário e o catálogo de telas são assim");

            var wrong = new List<string>();

            foreach (var endpoint in sessionOnly)
            {
                foreach (var (profile, email) in Crew)
                {
                    var client = await api.ClientOfAsync(email);
                    var answer = await Call(client, endpoint.Method, endpoint.Url, endpoint.Consumes);

                    if (answer.StatusCode == HttpStatusCode.Forbidden)
                    {
                        wrong.Add($"{endpoint} recusou {profile}");
                    }
                }
            }

            // Exigir tela aqui trancaria a pessoa para fora da própria chamada que diz quais
            // telas ela tem.
            wrong.Should().BeEmpty();
        }

        [Fact]
        public void TheSweepFindsTheWholeApi()
        {
            // Guarda da guarda: uma refatoração que parasse de achar os controladores
            // transformaria toda a matriz acima em aprovação silenciosa.
            ApiEndpoints.All.Should().HaveCountGreaterThan(55);

            ApiEndpoints.All.Should().OnlyHaveUniqueItems();

            ApiEndpoints.All.Where(endpoint => endpoint.Anonymous)
                .Select(endpoint => endpoint.Name)
                .Should().BeEquivalentTo(
                    ["AuthController.Login", "AuthController.Refresh"],
                    "porta aberta é decisão, e cada uma delas está escrita aqui");
        }

        /// <summary>
        /// Chama o endpoint com o corpo mais vazio que ele aceita.
        ///
        /// O conteúdo é vazio de propósito: a autorização acontece antes da leitura do corpo,
        /// então um corpo válido não mudaria a resposta — e mudaria o risco, porque aí a chamada
        /// passaria a fazer alguma coisa.
        ///
        /// O <b>tipo</b> do corpo, esse sim, importa. <c>[Consumes("multipart/form-data")]</c>
        /// participa da escolha da ação, e não do pipeline: mandar JSON para um endpoint de
        /// upload devolve <b>415 antes de a autorização rodar</b>. Um teste que mandasse o corpo
        /// errado ali provaria a recusa do roteamento, e sairia verde sem nunca ter tocado na
        /// fechadura — que é o pior resultado possível para um teste de segurança.
        /// </summary>
        /// <param name="client">Quem chama.</param>
        /// <param name="method">Verbo.</param>
        /// <param name="url">Endereço.</param>
        /// <param name="consumes">Tipo de conteúdo que o endpoint exige, quando exige.</param>
        /// <returns>A resposta.</returns>
        private static async Task<HttpResponseMessage> Call(
            HttpClient client,
            string method,
            string url,
            string? consumes)
        {
            using var request = new HttpRequestMessage(new HttpMethod(method), new Uri(url, UriKind.Relative));

            if (method is "POST" or "PUT" or "PATCH")
            {
                request.Content = consumes switch
                {
                    "multipart/form-data" => Multipart(),
                    _ => new StringContent("{}", Encoding.UTF8, "application/json"),
                };
            }

            // Aguardado aqui dentro de propósito: devolver a tarefa descartaria a requisição
            // antes de o envio terminar, e o corpo sumiria debaixo dele.
            return await client.SendAsync(request);
        }

        /// <summary>Um envio de arquivo com um byte, só para a rota reconhecer a chamada.</summary>
        /// <returns>O conteúdo.</returns>
        private static MultipartFormDataContent Multipart()
        {
            var file = new ByteArrayContent([0]);
            file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            return new MultipartFormDataContent { { file, "file", "vazio.bin" } };
        }
    }
}
