using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using RevendaPro.Domain.Interfaces.Security;

namespace RevendaPro.Api.Security
{
    /// <summary>
    /// A fronteira do parceiro (M24): quem está preso a um pátio alcança uma lista curta de
    /// leituras, e mais nada.
    ///
    /// Ele roda **para toda requisição autenticada**, e jamais só para as que passam pelo
    /// <c>RequireScreen</c>. Cobrir "exatamente o que importa hoje" é o raciocínio que o M12
    /// documentou como defeito: oito handlers liam por código sem filtrar a empresa, e um deles
    /// já conferia, porque alguém tinha olhado só aquele caminho.
    ///
    /// Faz duas coisas, nesta ordem:
    ///
    /// <list type="number">
    /// <item>
    /// resolve o pátio da pessoa e o deixa em <see cref="HttpContext.Items"/>, de onde o
    /// <c>CurrentUser</c> o lê e o <b>repositório de veículo</b> o aplica sozinho — assim a
    /// fronteira jamais depende de um handler lembrar dela;
    /// </item>
    /// <item>
    /// recusa, com <b>403</b>, tudo o que não estiver na lista declarada abaixo.
    /// </item>
    /// </list>
    ///
    /// A lista é uma <b>permissão explícita</b>, e não uma proibição: o endpoint escrito amanhã
    /// nasce recusado para o parceiro, e só passa quando alguém escrever que ele pode. É a única
    /// forma de a fronteira envelhecer bem — uma lista de proibições envelhece em silêncio, que
    /// é exatamente o defeito que o M12 provou existir.
    ///
    /// A leitura do pátio é do banco, com o mesmo cache do serviço de permissão e a mesma
    /// invalidação: prender ou soltar alguém vale na requisição seguinte. Uma claim no token
    /// faria a mudança esperar o token expirar.
    /// </summary>
    public sealed partial class YardScopeMiddleware(RequestDelegate next)
    {
        /// <summary>Onde o pátio da requisição fica guardado.</summary>
        public const string ItemKey = "revendapro:yard-scope";

        /// <summary>
        /// O que uma pessoa presa a um pátio alcança. Cada linha diz por quê, para entrar aqui
        /// ser uma decisão que alguém escreveu.
        /// </summary>
        private static readonly (string Method, Regex Path, string Why)[] Allowed =
        [
            ("POST", Login(), "entrar, renovar e sair: sem isto ela sequer tem sessão"),
            ("GET", Me(), "a sessão e o menu, que já vêm recortados pelo perfil"),
            ("GET", VehicleList(), "a listagem, que o repositório já entrega filtrada pelo pátio"),
            ("GET", VehicleFile(), "a ficha de um carro, que responde 404 fora do pátio dela"),
            ("GET", VehiclePhotos(), "as fotos daquele carro, que é o que ele mostra a quem pergunta")
        ];

        /// <summary>Runs the middleware.</summary>
        /// <param name="context">The request.</param>
        /// <param name="currentUser">Quem está chamando.</param>
        /// <param name="permissions">O alcance da pessoa.</param>
        /// <returns>The task.</returns>
        public async Task InvokeAsync(
            HttpContext context,
            ICurrentUser currentUser,
            IPermissionService permissions)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(currentUser);
            ArgumentNullException.ThrowIfNull(permissions);

            if (!currentUser.IsAuthenticated)
            {
                await next(context).ConfigureAwait(false);
                return;
            }

            var restriction = await permissions
                .GetYardRestrictionAsync(currentUser.Id, context.RequestAborted)
                .ConfigureAwait(false);

            if (restriction is null)
            {
                await next(context).ConfigureAwait(false);
                return;
            }

            context.Items[ItemKey] = restriction.Value;

            if (Reaches(context.Request))
            {
                await next(context).ConfigureAwait(false);
                return;
            }

            context.Response.StatusCode = StatusCodes.Status403Forbidden;

            await context.Response.WriteAsJsonAsync(
                new ProblemDetails
                {
                    Status = StatusCodes.Status403Forbidden,
                    Title = "Sem permissão",
                    Detail = "Quem enxerga um pátio alcança os carros que estão nele, e mais nada.",
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.5.3",
                    Instance = context.Request.Path
                },
                context.RequestAborted).ConfigureAwait(false);
        }

        private static bool Reaches(HttpRequest request)
        {
            var path = request.Path.Value ?? string.Empty;

            foreach (var (method, pattern, _) in Allowed)
            {
                if (string.Equals(request.Method, method, StringComparison.OrdinalIgnoreCase)
                    && pattern.IsMatch(path))
                {
                    return true;
                }
            }

            return false;
        }

        [GeneratedRegex(@"^/api/auth/(login|refresh|logout)/?$", RegexOptions.IgnoreCase)]
        private static partial Regex Login();

        [GeneratedRegex(@"^/api/auth/me/?$", RegexOptions.IgnoreCase)]
        private static partial Regex Me();

        [GeneratedRegex(@"^/api/vehicles/?$", RegexOptions.IgnoreCase)]
        private static partial Regex VehicleList();

        [GeneratedRegex(@"^/api/vehicles/[0-9a-f-]{36}/?$", RegexOptions.IgnoreCase)]
        private static partial Regex VehicleFile();

        [GeneratedRegex(@"^/api/vehicles/[0-9a-f-]{36}/photos/?$", RegexOptions.IgnoreCase)]
        private static partial Regex VehiclePhotos();
    }
}
