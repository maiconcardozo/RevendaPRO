using Foundation.Domain.Abstractions;
using System.Diagnostics;
using RevendaPro.Domain.ValueObjects;

namespace RevendaPro.Domain.Entities
{
    /// <summary>
    /// What the reference table said about one model, in one month.
    ///
    /// Global to the system, and not owned by a tenant: the table is public reference data,
    /// the same number for every dealership. That is also what makes ten cars of the same
    /// model — across two companies — cost a single query. Same shape as
    /// <see cref="Screen"/>, and for the same reason. See ADR-0005.
    ///
    /// <b>A quote never changes.</b> The month is closed the moment the table is published,
    /// so the row is a historical fact: there is no method here that writes a value, and a
    /// test holds that shut. This is what answers "sold for R$ 60.000 when the table of that
    /// month said R$ 56.815" years later, without the number being copied into the sale —
    /// which is exactly how the cost of the M6 had gone wrong.
    /// </summary>
    [DebuggerDisplay("FipeCode={FipeCode}, YearFuel={YearFuel}, ReferenceMonth={ReferenceMonth}")]
    public class FipeQuote : Entity
    {
        private FipeQuote() { }

        /// <summary>Code of the model in the table, as the table prints it.</summary>
        public string FipeCode { get; private set; } = string.Empty;

        /// <summary>
        /// Year and fuel of the exact priced row (<c>2014-5</c>).
        ///
        /// The year alone is ambiguous: the same model and year exist as flex and as petrol,
        /// at different prices. The pair is what the table prices.
        /// </summary>
        public string YearFuel { get; private set; } = string.Empty;

        /// <summary>
        /// First day of the month this quote belongs to. A table is monthly, so the day
        /// carries no meaning and is always the first — which makes two readings of the same
        /// month comparable.
        /// </summary>
        public DateOnly ReferenceMonth { get; private set; }

        /// <summary>The value, in decimal. Money is never a floating point number (RNF-12).</summary>
        public decimal Value { get; private set; }

        public short ModelYear { get; private set; }

        /// <summary>Brand as the table writes it (<c>GM - Chevrolet</c>).</summary>
        public string Brand { get; private set; } = string.Empty;

        /// <summary>Model as the table writes it, version included.</summary>
        public string Model { get; private set; } = string.Empty;

        /// <summary>Keeps what the table answered.</summary>
        /// <param name="price">The reading, already in decimal and with a month.</param>
        /// <param name="createdBy">Who kept it.</param>
        /// <returns>The quote.</returns>
        public static FipeQuote Create(FipePrice price, string createdBy = SystemActor)
        {
            ArgumentNullException.ThrowIfNull(price);

            return Create(
                price.FipeCode,
                price.YearFuel,
                price.Reference,
                price.Value,
                price.ModelYear,
                price.Brand,
                price.Model,
                createdBy);
        }

        /// <summary>Keeps one value of the table.</summary>
        /// <param name="fipeCode">Code of the model.</param>
        /// <param name="yearFuel">Year and fuel of the priced row.</param>
        /// <param name="referenceMonth">Month the value belongs to.</param>
        /// <param name="value">The value.</param>
        /// <param name="modelYear">Model year, so a listing filters without parsing the pair.</param>
        /// <param name="brand">Brand as the table writes it.</param>
        /// <param name="model">Model as the table writes it.</param>
        /// <param name="createdBy">Who kept it.</param>
        /// <returns>The quote.</returns>
        /// <remarks>
        /// Refuses with an argument exception, and never with a business rule: nobody types a
        /// quote. It is written by the automatic lookup, and a value of zero arriving here
        /// means the adapter let through something it should have refused.
        /// </remarks>
        public static FipeQuote Create(
            string fipeCode,
            string yearFuel,
            DateOnly referenceMonth,
            decimal value,
            short modelYear,
            string? brand,
            string? model,
            string createdBy = SystemActor)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(fipeCode);
            ArgumentException.ThrowIfNullOrWhiteSpace(yearFuel);
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, 0m);

            var quote = new FipeQuote
            {
                FipeCode = fipeCode.Trim(),
                YearFuel = yearFuel.Trim(),

                // Normalised to the first day, always: the source writes the month, and two
                // readings of the same table have to land on the same date to be comparable.
                ReferenceMonth = new DateOnly(referenceMonth.Year, referenceMonth.Month, 1),
                Value = value,
                ModelYear = modelYear,
                Brand = brand?.Trim() ?? string.Empty,
                Model = model?.Trim() ?? string.Empty
            };

            quote.SetCreatedBy(createdBy);

            return quote;
        }
    }
}
