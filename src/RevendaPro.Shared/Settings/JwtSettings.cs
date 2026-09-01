namespace RevendaPro.Shared.Settings
{
    /// <summary>JWT settings, bound from configuration.</summary>
    public class JwtSettings
    {
        /// <summary>Configuration section name.</summary>
        public const string SectionName = "Jwt";

        /// <summary>Signing key. At least 32 characters.</summary>
        public string Key { get; set; } = string.Empty;

        public string Issuer { get; set; } = "RevendaPro";

        public string Audience { get; set; } = "RevendaPro.Frontend";

        public int AccessTokenMinutes { get; set; } = 15;

        public int RefreshTokenDays { get; set; } = 7;
    }
}
