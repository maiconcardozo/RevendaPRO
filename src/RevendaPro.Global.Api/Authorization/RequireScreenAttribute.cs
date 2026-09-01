using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using RevendaPro.Global.Domain.Interfaces.Security;

namespace RevendaPro.Global.Api.Authorization
{
    /// <summary>
    /// Requires the authenticated user to hold the given screen.
    ///
    /// Independent of the menu: hiding a sidebar item is presentation, not security.
    /// Calling the route directly without the screen returns 403. See ADR-0002.
    /// </summary>
    public sealed class RequireScreenAttribute : TypeFilterAttribute
    {
        /// <summary>Creates the filter for a screen key.</summary>
        /// <param name="screenKey">Key of the required screen.</param>
        public RequireScreenAttribute(string screenKey)
            : base(typeof(ScreenFilter))
        {
            Arguments = [screenKey];
        }

        private sealed class ScreenFilter(string screenKey, IPermissionService permissionService)
            : IAsyncAuthorizationFilter
        {
            public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
            {
                ArgumentNullException.ThrowIfNull(context);

                var currentUser = context.HttpContext.RequestServices
                    .GetRequiredService<ICurrentUser>();

                if (!currentUser.IsAuthenticated)
                {
                    context.Result = new UnauthorizedResult();
                    return;
                }

                var keys = await permissionService
                    .GetScreenKeysAsync(currentUser.Id, context.HttpContext.RequestAborted)
                    .ConfigureAwait(false);

                if (keys.Contains(screenKey))
                {
                    return;
                }

                context.Result = new ObjectResult(new ProblemDetails
                {
                    Status = StatusCodes.Status403Forbidden,
                    Title = "Sem permissão",
                    Detail = $"Esta tela depende de liberação para o perfil \"{screenKey}\".",
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.5.3",
                    Instance = context.HttpContext.Request.Path
                })
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };
            }
        }
    }
}
