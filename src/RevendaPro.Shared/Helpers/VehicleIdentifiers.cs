using System.Text.RegularExpressions;

namespace RevendaPro.Shared.Helpers
{
    /// <summary>
    /// Validates what identifies a vehicle: the plate and the chassis.
    ///
    /// Both are stored bare — uppercase, without the hyphen, without spaces. Formatting is a
    /// screen concern, the same rule already applied to CPF and phone.
    /// </summary>
    public static partial class VehicleIdentifiers
    {
        /// <summary>
        /// Strips everything that is not a letter or a digit and uppercases the rest.
        /// </summary>
        /// <param name="value">Plate or chassis, formatted or not.</param>
        /// <returns>The bare value, or an empty string.</returns>
        public static string Normalize(string? value) =>
            string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : new string([.. value.Where(char.IsLetterOrDigit)]).ToUpperInvariant();

        /// <summary>
        /// Whether the plate is valid, in either Brazilian format.
        ///
        /// One expression covers both, and that is the point: the old format is three letters
        /// and four digits (ABC1234), and Mercosul turns the fifth character into a letter
        /// (ABC1D23). Everything else is identical, so a single pattern accepts a plate from
        /// 1990 and one issued today.
        /// </summary>
        /// <param name="value">Plate, formatted or not.</param>
        /// <returns>True when the plate matches either format.</returns>
        public static bool IsValidPlate(string? value) => PlatePattern().IsMatch(Normalize(value));

        /// <summary>
        /// Whether the chassis is a well formed VIN: seventeen characters, and never
        /// <c>I</c>, <c>O</c> or <c>Q</c>.
        /// </summary>
        /// <param name="value">Chassis, formatted or not.</param>
        /// <returns>True when the chassis is well formed.</returns>
        /// <remarks>
        /// The three excluded letters are excluded by the standard itself, to keep them from
        /// being read as <c>1</c> and <c>0</c> — which is exactly the mistake somebody makes
        /// copying seventeen characters off a windscreen.
        /// <para>
        /// The check digit is deliberately left alone. It is mandatory in North America and
        /// applied loosely elsewhere, so demanding it would refuse cars that exist and are
        /// perfectly sellable.
        /// </para>
        /// </remarks>
        public static bool IsValidChassis(string? value) => ChassisPattern().IsMatch(Normalize(value));

        [GeneratedRegex("^[A-Z]{3}[0-9][0-9A-Z][0-9]{2}$")]
        private static partial Regex PlatePattern();

        [GeneratedRegex("^[A-HJ-NPR-Z0-9]{17}$")]
        private static partial Regex ChassisPattern();
    }
}
