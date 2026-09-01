using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using RevendaPro.Global.Domain.Interfaces.Security;
using RevendaPro.Global.Shared.Constants;

namespace RevendaPro.Global.Api.Security
{
    /// <summary>
    /// Reads the caller of the current request from the access token claims.
    ///
    /// The token carries sub, user_code and tenant_id only: screen keys are resolved per
    /// request, so a permission change applies immediately. See ADR-0002.
    /// </summary>
    public class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
    {
        private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

        /// <inheritdoc/>
        public bool IsAuthenticated =>
            Principal?.Identity?.IsAuthenticated == true && Id > 0;

        /// <inheritdoc/>
        public int Id => ReadInt(ClaimTypes.NameIdentifier, JwtRegisteredClaimNames.Sub);

        /// <inheritdoc/>
        public Guid Code =>
            Guid.TryParse(Principal?.FindFirst(TokenClaims.UserCode)?.Value, out var code)
                ? code
                : Guid.Empty;

        /// <inheritdoc/>
        public int TenantId => ReadInt(TokenClaims.TenantId);

        private int ReadInt(params string[] claimTypes)
        {
            foreach (var claimType in claimTypes)
            {
                var value = Principal?.FindFirst(claimType)?.Value;

                if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                {
                    return parsed;
                }
            }

            return 0;
        }
    }
}
