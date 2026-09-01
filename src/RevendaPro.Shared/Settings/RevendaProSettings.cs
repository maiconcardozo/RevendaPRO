namespace RevendaPro.Shared.Settings
{
    /// <summary>Database settings, bound from configuration.</summary>
    public class RevendaProSettings
    {
        /// <summary>Configuration section name.</summary>
        public const string SectionName = "RevendaPro";

        /// <summary>MariaDB connection string.</summary>
        public string ConnectionString { get; set; } = string.Empty;

        /// <summary>Tenant created on first run.</summary>
        public string PilotTenant { get; set; } = "Revenda Piloto";

        /// <summary>E-mail of the administrator created on first run.</summary>
        public string AdminEmail { get; set; } = string.Empty;

        /// <summary>Password of the administrator created on first run.</summary>
        public string AdminPassword { get; set; } = string.Empty;

        /// <summary>Folder where user photos are stored, outside the database.</summary>
        public string PhotoFolder { get; set; } = "/app/files/photos";
    }
}
