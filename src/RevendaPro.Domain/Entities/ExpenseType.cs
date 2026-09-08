using System.Diagnostics;
using RevendaPro.Domain.Enums;
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

        /// <summary>
        /// Para onde este tipo serve (M22): o carro, a loja, ou os dois. Todo tipo que existia
        /// antes da despesa da loja nasceu de carro, porque é o que ele é.
        /// </summary>
        public ExpenseScope Scope { get; private set; } = ExpenseScope.Vehicle;

        /// <summary>Se este tipo pode ser escolhido num gasto de carro.</summary>
        public bool ServesVehicles => (Scope & ExpenseScope.Vehicle) != 0;

        /// <summary>Se este tipo pode ser escolhido numa despesa da loja.</summary>
        public bool ServesStore => (Scope & ExpenseScope.Store) != 0;

        /// <summary>Creates a type of expense.</summary>
        /// <param name="idTenant">Owning tenant.</param>
        /// <param name="name">Name shown to the user.</param>
        /// <param name="keywords">Words that point an expense here.</param>
        /// <param name="position">Position in the list.</param>
        /// <param name="createdBy">Who created it.</param>
        /// <param name="scope">Para onde serve: o carro, a loja, ou os dois (M22).</param>
        /// <returns>The type.</returns>
        public static ExpenseType Create(
            int idTenant,
            string name,
            string? keywords = null,
            int position = 0,
            string createdBy = SystemActor,
            ExpenseScope scope = ExpenseScope.Vehicle)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new BusinessRuleException("Informe o nome do tipo de gasto.");
            }

            var type = new ExpenseType(idTenant)
            {
                Name = name.Trim(),
                Keywords = Normalize(keywords),
                Position = position,
                Scope = Valid(scope)
            };

            type.SetCreatedBy(createdBy);

            return type;
        }

        /// <summary>Renames the type or changes its words.</summary>
        /// <param name="name">Name shown to the user.</param>
        /// <param name="keywords">Words that point an expense here.</param>
        /// <param name="position">Position in the list.</param>
        /// <param name="updatedBy">Who changed it.</param>
        /// <param name="scope">Para onde serve: o carro, a loja, ou os dois (M22).</param>
        public void Update(
            string name,
            string? keywords,
            int position,
            string updatedBy = SystemActor,
            ExpenseScope scope = ExpenseScope.Vehicle)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new BusinessRuleException("Informe o nome do tipo de gasto.");
            }

            Name = name.Trim();
            Keywords = Normalize(keywords);
            Position = position;
            Scope = Valid(scope);

            UpdateAuditInfo(updatedBy);
        }

        /// <summary>
        /// Um escopo fora dos três valores é recusado. Sem isto, um zero vindo de um corpo JSON
        /// mal preenchido criaria um tipo que jamais aparece em lista nenhuma — um cadastro
        /// invisível, que é pior do que um erro.
        /// </summary>
        private static ExpenseScope Valid(ExpenseScope scope) =>
            scope is ExpenseScope.Vehicle or ExpenseScope.Store or ExpenseScope.Both
                ? scope
                : throw new BusinessRuleException("Escolha se o tipo serve para o carro, para a loja, ou para os dois.");

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
