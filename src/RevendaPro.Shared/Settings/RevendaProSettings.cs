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

        /// <summary>
        /// Creates the demonstration users on first run. **Off unless explicitly turned on**,
        /// so a production database never gets them.
        /// </summary>
        public bool SeedDemoUsers { get; set; }

        /// <summary>
        /// Password shared by the demonstration users. Required when
        /// <see cref="SeedDemoUsers"/> is on; the seeding refuses to run without it, instead
        /// of falling back to something guessable.
        /// </summary>
        public string DemoPassword { get; set; } = string.Empty;

        /// <summary>
        /// Creates the demonstration yard — four places and twenty cars — on first run.
        ///
        /// **Off unless explicitly turned on**, like the demonstration users, so a production
        /// database never gets them. It exists so a fresh development machine already shows a
        /// yard with profit, loss, cars in every stage and — on purpose — the two ends of the
        /// FIPE search: the name that matches one line of the table, and the name typed in a
        /// hurry that matches twenty.
        /// </summary>
        public bool SeedDemoVehicles { get; set; }
    }
}
