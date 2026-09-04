namespace RevendaPro.Shared.Settings
{
    /// <summary>
    /// Where the reference table is read from. <b>The only place in the repository that knows
    /// which mirror answers</b> — and it knows it as values, never as code.
    ///
    /// FIPE publishes no API, so every option is a third party mirror. The one configured by
    /// default answers 500 queries a day with no token and 1.000 with a free one, which is
    /// more than a year of this operation in a single day: the table changes once a month, the
    /// yard holds dozens of cars, and quotes are stored per model. See ADR-0005.
    /// </summary>
    public class FipeSettings
    {
        /// <summary>Configuration section name.</summary>
        public const string SectionName = "Fipe";

        /// <summary>
        /// Whether the automatic query is used at all. Off leaves the system exactly as the M8
        /// left it: the value is typed by hand, and nothing reaches the network.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>Base address of the mirror, with no trailing slash.</summary>
        public string BaseUrl { get; set; } = "https://fipe.parallelum.com.br/api/v2";

        /// <summary>
        /// Path of the vehicle kind this system prices. The mirror also serves motorcycles and
        /// trucks; the operation deals in cars, and the day it deals in more, this is a value.
        /// </summary>
        public string VehicleType { get; set; } = "cars";

        /// <summary>
        /// Free subscription token, when there is one. Empty works, with a lower daily
        /// allowance. Secret: it lives in the environment, and never in the repository.
        /// </summary>
        public string Token { get; set; } = string.Empty;

        /// <summary>
        /// How long to wait for the mirror. Short on purpose: nobody saving a vehicle should
        /// wait on a reference table, and a slow answer is the same as no answer.
        /// </summary>
        public int TimeoutInSeconds { get; set; } = 8;

        /// <summary>
        /// Whether the yard updates itself. Off leaves the lookup working by hand, one car at
        /// a time, which is exactly what the V3 button does.
        /// </summary>
        public bool RefreshYard { get; set; } = true;

        /// <summary>
        /// How often the routine looks at the yard.
        ///
        /// The table changes <b>once a month</b>, so this is not a schedule — it is how long a
        /// newly published table may take to reach the screens. Six hours costs four wake-ups
        /// a day, and a wake-up with nothing to do is a single call to the source.
        /// </summary>
        public int RefreshEveryHours { get; set; } = 6;

        /// <summary>
        /// How long to wait before the first run, so the routine stays out of the way while
        /// the API is still applying migrations and answering its first requests.
        /// </summary>
        public int RefreshFirstRunAfterSeconds { get; set; } = 60;
    }
}
