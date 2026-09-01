namespace RevendaPro.Global.Shared.Constants
{
    /// <summary>Custom claims carried by the access token.</summary>
    public static class TokenClaims
    {
        /// <summary>Public identifier of the user, exposed instead of the internal Id.</summary>
        public const string UserCode = "user_code";

        /// <summary>Tenant the user belongs to, used to isolate every query.</summary>
        public const string TenantId = "tenant_id";
    }
}
