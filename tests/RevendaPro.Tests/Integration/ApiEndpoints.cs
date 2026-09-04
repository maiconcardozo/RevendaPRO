using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using RevendaPro.Api.Authorization;
using RevendaPro.Api.Controllers;

namespace RevendaPro.Tests.Integration
{
    /// <summary>
    /// Um endpoint, pronto para ser chamado.
    /// </summary>
    /// <param name="Name">Controlador e ação, para a mensagem de falha dizer onde olhar.</param>
    /// <param name="Method">Verbo HTTP.</param>
    /// <param name="Url">Endereço já montado, com identificadores de mentira.</param>
    /// <param name="Screen">A tela que ele exige, ou nulo quando basta a sessão.</param>
    /// <param name="Anonymous">Se ele é aberto por decisão.</param>
    /// <param name="Consumes">
    /// O tipo de conteúdo que ele aceita, quando ele exige um.
    ///
    /// Importa mais do que parece: <c>[Consumes]</c> participa da <b>escolha da ação</b>, e não
    /// do pipeline. Chamar um endpoint de upload com JSON devolve <b>415 antes da
    /// autorização</b> — sem 401 e sem 403 —, então um teste que mandasse o corpo errado
    /// provaria a recusa do roteamento, e jamais a da fechadura.
    /// </param>
    public sealed record ApiEndpoint(
        string Name,
        string Method,
        string Url,
        string? Screen,
        bool Anonymous,
        string? Consumes)
    {
        /// <inheritdoc/>
        public override string ToString() => $"{Method} {Url}";
    }

    /// <summary>
    /// Todos os endpoints da API, achados percorrendo a montagem.
    ///
    /// <b>Nada aqui é escrito à mão.</b> Uma lista de endereços digitada envelhece no primeiro
    /// endpoint novo, e envelhece em silêncio — passando verde justamente onde deveria falhar.
    /// Percorrendo a montagem, o endpoint criado amanhã entra na matriz no dia em que nasce, que
    /// é a mesma razão pela qual o <c>ApiGuardTests</c> faz isso desde o marco de acesso.
    /// </summary>
    public static partial class ApiEndpoints
    {
        private static readonly Assembly Api = typeof(AuthController).Assembly;

        /// <summary>Todo endpoint da API, com o endereço pronto para chamar.</summary>
        public static IReadOnlyList<ApiEndpoint> All { get; } = Discover();

        private static List<ApiEndpoint> Discover()
        {
            var found = new List<ApiEndpoint>();

            var controllers = Api.GetTypes()
                .Where(type => typeof(ControllerBase).IsAssignableFrom(type) && !type.IsAbstract);

            foreach (var controller in controllers)
            {
                var prefix = controller.GetCustomAttribute<RouteAttribute>()?.Template ?? string.Empty;
                var controllerScreen = ScreenOf(controller.GetCustomAttributes());
                var controllerAnonymous = controller.GetCustomAttribute<AllowAnonymousAttribute>() is not null;

                var actions = controller
                    .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .Where(method => !method.IsSpecialName);

                foreach (var action in actions)
                {
                    foreach (var http in action.GetCustomAttributes<HttpMethodAttribute>())
                    {
                        found.Add(new ApiEndpoint(
                            $"{controller.Name}.{action.Name}",
                            http.HttpMethods.First(),
                            Address(prefix, http.Template),
                            ScreenOf(action.GetCustomAttributes()) ?? controllerScreen,
                            controllerAnonymous
                                || action.GetCustomAttribute<AllowAnonymousAttribute>() is not null,
                            ContentTypeOf(action)));
                    }
                }
            }

            return [.. found.OrderBy(endpoint => endpoint.Url, StringComparer.Ordinal)
                            .ThenBy(endpoint => endpoint.Method, StringComparer.Ordinal)];
        }

        /// <summary>
        /// O tipo de conteúdo que a ação aceita.
        ///
        /// Vale ler o <c>[Consumes]</c> <b>e</b> os parâmetros: um <c>IFormFile</c> faz o
        /// framework inferir multipart mesmo sem o atributo, e foi assim que o envio de foto do
        /// usuário respondeu 415 a um teste que se achava certo. Perguntar aos dois deixa o
        /// varredor certo também para o endpoint que alguém escrever amanhã sem o atributo.
        /// </summary>
        /// <param name="action">A ação.</param>
        /// <returns>O tipo de conteúdo, ou nulo quando ela aceita JSON.</returns>
        private static string? ContentTypeOf(MethodInfo action)
        {
            var declared = action.GetCustomAttribute<ConsumesAttribute>()?.ContentTypes.FirstOrDefault();

            if (declared is not null)
            {
                return declared;
            }

            var sendsFile = action.GetParameters().Any(parameter =>
                typeof(IFormFile).IsAssignableFrom(parameter.ParameterType)
                || typeof(IFormFileCollection).IsAssignableFrom(parameter.ParameterType));

            return sendsFile ? "multipart/form-data" : null;
        }

        /// <summary>
        /// A chave da tela exigida.
        ///
        /// Ela mora nos argumentos do filtro, e não numa propriedade: o
        /// <see cref="RequireScreenAttribute"/> é um <c>TypeFilterAttribute</c>, e é assim que
        /// ele entrega a chave ao filtro que a confere.
        /// </summary>
        /// <param name="attributes">Atributos do controlador ou da ação.</param>
        /// <returns>A chave, ou nulo.</returns>
        private static string? ScreenOf(IEnumerable<Attribute> attributes) =>
            attributes.OfType<RequireScreenAttribute>()
                .Select(attribute => attribute.Arguments?.FirstOrDefault() as string)
                .FirstOrDefault(key => key is not null);

        /// <summary>
        /// Monta o endereço a partir dos dois pedaços do gabarito, e troca cada parâmetro de
        /// rota por um valor de mentira.
        ///
        /// Os identificadores são <b>aleatórios de propósito</b>: com eles a matriz bate na
        /// fechadura sem entrar na casa — nenhuma linha é criada, e nenhuma é destruída.
        /// </summary>
        /// <param name="prefix">Gabarito do controlador.</param>
        /// <param name="template">Gabarito da ação.</param>
        /// <returns>O endereço.</returns>
        private static string Address(string prefix, string? template)
        {
            var whole = string.IsNullOrWhiteSpace(template)
                ? prefix
                : template.StartsWith('/') ? template : $"{prefix}/{template}";

            return "/" + Fill(whole).Trim('/');
        }

        private static string Fill(string template) =>
            RouteParameter().Replace(template, match =>
            {
                var constraint = match.Groups["constraint"].Value;

                return constraint switch
                {
                    "guid" => Guid.NewGuid().ToString(),
                    "int" => 1.ToString(CultureInfo.InvariantCulture),

                    // Marca e modelo da tabela de referência chegam como texto.
                    _ => "1",
                };
            });

        [GeneratedRegex(@"\{(?<name>[A-Za-z0-9_]+)(:(?<constraint>[A-Za-z0-9]+))?\??\}")]
        private static partial Regex RouteParameter();
    }
}
