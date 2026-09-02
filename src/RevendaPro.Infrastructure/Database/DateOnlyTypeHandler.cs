using System.Data;
using Dapper;

namespace RevendaPro.Infrastructure.Database
{
    /// <summary>
    /// Teaches Dapper to pass a <see cref="DateOnly"/> as a parameter and to read one back.
    ///
    /// Without it, saving a purchase date fails with "The member PurchaseDate of type
    /// System.DateOnly cannot be used as a parameter value" — Dapper predates the type and
    /// carries no mapping for it.
    ///
    /// This is the same gap <c>GuidTypeHandler</c> fills in Foundation, and it belongs there
    /// for the same reason: any project storing a date without a time hits it. Registered here
    /// meanwhile, and it moves to the package on the next release.
    /// </summary>
    public sealed class DateOnlyTypeHandler : SqlMapper.TypeHandler<DateOnly>
    {
        /// <inheritdoc/>
        public override void SetValue(IDbDataParameter parameter, DateOnly value)
        {
            ArgumentNullException.ThrowIfNull(parameter);

            parameter.DbType = DbType.Date;
            parameter.Value = value.ToDateTime(TimeOnly.MinValue);
        }

        /// <inheritdoc/>
        public override DateOnly Parse(object value) => value switch
        {
            DateOnly date => date,
            DateTime moment => DateOnly.FromDateTime(moment),

            // MariaDB can answer a DATE column as a string depending on the connector
            // settings, so reading it back has to survive that too.
            string text => DateOnly.Parse(text, System.Globalization.CultureInfo.InvariantCulture),

            _ => throw new InvalidCastException(
                $"Cannot convert {value?.GetType().Name ?? "null"} to DateOnly.")
        };
    }
}
