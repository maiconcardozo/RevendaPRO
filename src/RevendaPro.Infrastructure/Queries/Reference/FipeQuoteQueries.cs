using Foundation.Dapper.Sql;

namespace RevendaPro.Infrastructure.Queries.Reference
{
    /// <summary>
    /// Columns of FipeQuote, so every query returns the same shape and Dapper can materialize
    /// the entity, audit state included.
    /// </summary>
    internal static class FipeQuoteColumns
    {
        public const string All = """
            Id, Code, FipeCode, YearFuel, ReferenceMonth, Value, ModelYear, Brand, Model,
            IsActive, DtCreated, CreatedBy, DtUpdated, UpdatedBy, DtDeleted, DeletedBy
            """;
    }

    /// <summary>
    /// One model in one month.
    ///
    /// The month arrives already on the first day, and the column is written the same way, so
    /// the comparison is an equality and the unique index answers it. See ADR-0005.
    /// </summary>
    internal sealed class FindFipeQuoteQuery : SqlQuery
    {
        public FindFipeQuoteQuery(string fipeCode, string yearFuel, DateOnly referenceMonth)
        {
            FipeCode = fipeCode;
            YearFuel = yearFuel;
            ReferenceMonth = new DateOnly(referenceMonth.Year, referenceMonth.Month, 1);
        }

        public string FipeCode { get; }

        public string YearFuel { get; }

        public DateOnly ReferenceMonth { get; }

        public override string GetSql() => $"""
            SELECT {FipeQuoteColumns.All}
            FROM FipeQuote
            WHERE FipeCode = @FipeCode
              AND YearFuel = @YearFuel
              AND ReferenceMonth = @ReferenceMonth
              AND IsActive = 1
            """;
    }

    /// <summary>Every month already kept for one model, newest first.</summary>
    internal sealed class ListFipeQuotesByModelQuery(string fipeCode, string yearFuel) : SqlQuery
    {
        public string FipeCode { get; } = fipeCode;

        public string YearFuel { get; } = yearFuel;

        public override string GetSql() => $"""
            SELECT {FipeQuoteColumns.All}
            FROM FipeQuote
            WHERE FipeCode = @FipeCode
              AND YearFuel = @YearFuel
              AND IsActive = 1
            ORDER BY ReferenceMonth DESC
            """;
    }
}
