using System.Globalization;
using System.Text;

namespace RevendaPro.Infrastructure.Reference
{
    /// <summary>
    /// Turning what the mirror writes for people into what the domain can compute with.
    ///
    /// The source answers in Brazilian Portuguese, formatted for reading: a price arrives as
    /// <c>"R$ 56.530,00"</c> and a table as <c>"setembro de 2026"</c> or
    /// <c>"setembro/2026"</c> — the same month written two ways by two endpoints of the same
    /// API. Both are parsed here, next to the adapter, and never spread through the system.
    /// </summary>
    internal static class FipeText
    {
        private static readonly CultureInfo Brazil = CultureInfo.GetCultureInfo("pt-BR");

        /// <summary>
        /// Month names as the source writes them, without accents, so that "março" and "marco"
        /// both land on March. Written out instead of read from culture data: this is the
        /// vocabulary of one API, and it stays readable next to the code that meets it.
        /// </summary>
        private static readonly string[] Months =
        [
            "janeiro", "fevereiro", "marco", "abril", "maio", "junho",
            "julho", "agosto", "setembro", "outubro", "novembro", "dezembro"
        ];

        /// <summary>
        /// Reads a price written for a person.
        ///
        /// Decimal, and never double: money in this system is decimal (RNF-12), and a binary
        /// floating point number cannot hold every cent.
        /// </summary>
        /// <param name="text">What the source sent (<c>"R$ 56.530,00"</c>).</param>
        /// <param name="value">The price.</param>
        /// <returns>True when the text was a price.</returns>
        public static bool TryParseMoney(string? text, out decimal value)
        {
            value = 0m;

            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            return decimal.TryParse(
                text,
                NumberStyles.Currency | NumberStyles.AllowThousands | NumberStyles.AllowDecimalPoint,
                Brazil,
                out value) && value > 0m;
        }

        /// <summary>
        /// Reads a reference month, in either shape the API uses.
        ///
        /// Returns the first day of the month, because a table is monthly: the day carries no
        /// meaning, and fixing it makes two readings of the same month comparable.
        /// </summary>
        /// <param name="text">What the source sent (<c>"setembro de 2026"</c>).</param>
        /// <param name="month">First day of that month.</param>
        /// <returns>True when the text was a month.</returns>
        public static bool TryParseMonth(string? text, out DateOnly month)
        {
            month = default;

            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            // "setembro de 2026" and "setembro/2026" reduce to the same two words.
            var parts = WithoutAccents(text)
                .Replace("/", " ", StringComparison.Ordinal)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            var name = Array.Find(parts, p => Array.Exists(Months, m => m.Equals(p, StringComparison.OrdinalIgnoreCase)));

            if (name is null)
            {
                return false;
            }

            var year = Array.Find(
                parts,
                p => p.Length == 4 && int.TryParse(p, CultureInfo.InvariantCulture, out _));

            if (year is null)
            {
                return false;
            }

            var index = Array.FindIndex(Months, m => m.Equals(name, StringComparison.OrdinalIgnoreCase));

            month = new DateOnly(int.Parse(year, CultureInfo.InvariantCulture), index + 1, 1);

            return true;
        }

        /// <summary>
        /// The model year inside a year-fuel code (<c>2014-5</c>).
        ///
        /// The table prices a zero kilometre car under the year 32000, which is a real value of
        /// this vocabulary and fits where it lands.
        /// </summary>
        /// <param name="yearFuel">The code.</param>
        /// <param name="modelYear">The year.</param>
        /// <returns>True when a year could be read.</returns>
        public static bool TryParseModelYear(string? yearFuel, out short modelYear)
        {
            modelYear = 0;

            if (string.IsNullOrWhiteSpace(yearFuel))
            {
                return false;
            }

            var head = yearFuel.Split('-', 2)[0];

            return short.TryParse(head, CultureInfo.InvariantCulture, out modelYear);
        }

        private static string WithoutAccents(string text)
        {
            var decomposed = text.Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(decomposed.Length);

            foreach (var character in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                {
                    builder.Append(character);
                }
            }

            return builder.ToString().Normalize(NormalizationForm.FormC);
        }
    }
}
