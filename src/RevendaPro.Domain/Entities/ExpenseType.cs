using System.Diagnostics;
using RevendaPro.Shared.Exceptions;

namespace RevendaPro.Domain.Entities
{
    /// <summary>
    /// A kind of expense, maintained by the dealership itself (RF-09).
    ///
    /// A table rather than an enum because the kinds that are missing only show up in use: a
    /// mirror, a power window, an air conditioning repair. With a fixed list all of those land
    /// in "other", which is where the breakdown stops being worth reading.
    /// </summary>
    [DebuggerDisplay("Name={Name}, IdTenant={IdTenant}")]
    public class ExpenseType : TenantEntity
    {
        private ExpenseType() { }

        private ExpenseType(int idTenant) : base(idTenant) { }

        /// <summary>Displayed to the user, and therefore written in Portuguese.</summary>
        public string Name { get; private set; } = string.Empty;

        /// <summary>
        /// Words that point an expense at this type, separated by commas.
        ///
        /// They live here, and not in a dictionary in the code, so that the suggestion keeps
        /// working for the types the dealership creates. A dictionary in the code would only
        /// ever serve the types somebody anticipated.
        /// </summary>
        public string? Keywords { get; private set; }

        /// <summary>Position in the list the user picks from.</summary>
        public int Position { get; private set; }

        /// <summary>Creates a type of expense.</summary>
        /// <param name="idTenant">Owning tenant.</param>
        /// <param name="name">Name shown to the user.</param>
        /// <param name="keywords">Words that point an expense here.</param>
        /// <param name="position">Position in the list.</param>
        /// <param name="createdBy">Who created it.</param>
        /// <returns>The type.</returns>
        public static ExpenseType Create(
            int idTenant,
            string name,
            string? keywords = null,
            int position = 0,
            string createdBy = SystemActor)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new BusinessRuleException("Informe o nome do tipo de gasto.");
            }

            var type = new ExpenseType(idTenant)
            {
                Name = name.Trim(),
                Keywords = Normalize(keywords),
                Position = position
            };

            type.SetCreatedBy(createdBy);

            return type;
        }

        /// <summary>Renames the type or changes its words.</summary>
        /// <param name="name">Name shown to the user.</param>
        /// <param name="keywords">Words that point an expense here.</param>
        /// <param name="position">Position in the list.</param>
        /// <param name="updatedBy">Who changed it.</param>
        public void Update(string name, string? keywords, int position, string updatedBy = SystemActor)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new BusinessRuleException("Informe o nome do tipo de gasto.");
            }

            Name = name.Trim();
            Keywords = Normalize(keywords);
            Position = position;

            UpdateAuditInfo(updatedBy);
        }

        /// <summary>
        /// Whether a description points at this type.
        ///
        /// Matching ignores case and accents, because somebody typing fast writes "mecanica"
        /// as often as "mecânica", and a suggestion that misses on an accent is a suggestion
        /// nobody trusts.
        /// </summary>
        /// <param name="description">What the user typed.</param>
        /// <returns>True when one of the words appears in the description.</returns>
        public bool Matches(string? description)
        {
            if (string.IsNullOrWhiteSpace(description) || string.IsNullOrWhiteSpace(Keywords))
            {
                return false;
            }

            var text = Fold(description);

            return Keywords
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(Fold)
                .Any(word => word.Length > 0 && text.Contains(word, StringComparison.Ordinal));
        }

        private static string? Normalize(string? keywords) =>
            string.IsNullOrWhiteSpace(keywords)
                ? null
                : string.Join(", ", keywords
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(word => word.ToLowerInvariant())
                    .Distinct(StringComparer.Ordinal));

        /// <summary>Lowercase and without accents, so "mecanica" finds "Mecânica".</summary>
        private static string Fold(string value) =>
            string.Concat(value
                .ToLowerInvariant()
                .Normalize(System.Text.NormalizationForm.FormD)
                .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
                            != System.Globalization.UnicodeCategory.NonSpacingMark));
    }
}
