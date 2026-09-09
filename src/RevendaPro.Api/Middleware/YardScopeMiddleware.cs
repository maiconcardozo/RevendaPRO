using RevendaPro.Domain.Interfaces.Security;

namespace RevendaPro.Api.Security
{
    /// <summary>
    /// Resolve, uma vez por requisição, o pátio a que quem está chamando está preso (M24).
    ///
    /// Ele roda **para toda requisição autenticada**, e jamais só para as que passam pelo
    /// <c>RequireScreen</c>. Cobrir "exatamente o que importa hoje" é o raciocínio que o M12
    /// documentou como defeito: oito handlers liam por código sem filtrar a empresa, e um deles
    /// já conferia, porque alguém tinha olhado só aquele caminho.
    ///
    /// O valor fica em <see cref="HttpContext.Items"/>, de onde o <c>CurrentUser</c> o lê e o
    /// repositório de veículo o aplica sozinho. Assim a fronteira jamais depende de um handler
    /// lembrar de passá-la.
    ///
    /// A leitura é do banco, com o mesmo cache do serviço de permissão e a mesma invalidação:
    /// prender ou soltar alguém vale na requisição seguinte. Uma claim no token faria a mudança
    /// esperar o token expirar.
    /// </summary>
    public sealed class YardScopeMiddleware(RequestDelegate next)
    {
        /// <summary>Onde o pátio da requisição fica guardado.</summary>
        public const string ItemKey = "revendapro:yard-scope";

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

            if (currentUser.IsAuthenticated)
            {
                var restriction = await permissions
                    .GetYardRestrictionAsync(currentUser.Id, context.RequestAborted)
                    .ConfigureAwait(false);

                if (restriction is not null)
                {
                    context.Items[ItemKey] = restriction.Value;
                }
            }

            await next(context).ConfigureAwait(false);
        }
    }
}
