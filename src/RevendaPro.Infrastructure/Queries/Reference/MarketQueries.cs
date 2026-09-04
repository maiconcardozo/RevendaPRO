using Foundation.Dapper.Sql;

namespace RevendaPro.Infrastructure.Queries.Reference
{
    /// <summary>
    /// Where every vehicle of a dealership stands against the reference table.
    ///
    /// <b>Each amount meets the quote of its own month.</b> The purchase meets the table of the
    /// month it was bought, the sale meets the table of the month it closed, and what is being
    /// asked meets the table of now. Comparing a deal from August against the table of today
    /// would measure the passage of time and call it a result.
    ///
    /// That is what the <c>FipeQuote</c> table was for: the quote of a closed month never
    /// changes, so this join answers the same thing years later — and no number is repeated
    /// inside the sale, which is how the cost of the M6 had gone wrong. See ADR-0005.
    ///
    /// The joins are all LEFT: a car with no code, a month never fetched or a deal older than
    /// this milestone come back with no reference, and the screen says so instead of inventing
    /// a number.
    /// </summary>
    internal sealed class ListMarketPositionsQuery : SqlQuery
    {
        public ListMarketPositionsQuery(int idTenant, DateOnly currentMonth, DateOnly today)
        {
            IdTenant = idTenant;
            CurrentMonth = new DateOnly(currentMonth.Year, currentMonth.Month, 1);
            PreviousMonth = CurrentMonth.AddMonths(-1);
            Today = today;
        }

        public int IdTenant { get; }

        public DateOnly CurrentMonth { get; }

        public DateOnly PreviousMonth { get; }

        public DateOnly Today { get; }

        public override string GetSql() => """
            SELECT
                v.Code,
                v.Plate,
                v.Brand,
                v.Model,
                v.Version,
                v.ModelYear,
                v.Status,
                -- O carro vendido para de contar no dia da venda: para ele a pergunta e
                -- quanto tempo ficou parado, e nao ha quanto tempo foi comprado.
                DATEDIFF(COALESCE(s.Date, @Today), v.PurchaseDate) AS DaysInStock,
                v.PurchasePrice,
                v.PurchaseDate,
                qp.Value AS PurchaseReference,
                v.DesiredNetPrice,
                qn.Value AS CurrentReference,
                qb.Value AS PreviousReference,
                s.Amount AS SaleAmount,
                s.Date AS SaleDate,
                qs.Value AS SaleReference
            FROM Vehicle v
            LEFT JOIN FipeQuote qp
                ON qp.FipeCode = v.FipeCode
               AND qp.YearFuel = v.FipeYearFuel
               AND qp.ReferenceMonth = v.PurchaseDate - INTERVAL (DAYOFMONTH(v.PurchaseDate) - 1) DAY
               AND qp.IsActive = 1
            LEFT JOIN FipeQuote qn
                ON qn.FipeCode = v.FipeCode
               AND qn.YearFuel = v.FipeYearFuel
               AND qn.ReferenceMonth = @CurrentMonth
               AND qn.IsActive = 1
            LEFT JOIN FipeQuote qb
                ON qb.FipeCode = v.FipeCode
               AND qb.YearFuel = v.FipeYearFuel
               AND qb.ReferenceMonth = @PreviousMonth
               AND qb.IsActive = 1
            LEFT JOIN Sale s
                ON s.IdVehicle = v.Id
               AND s.IsActive = 1
            LEFT JOIN FipeQuote qs
                ON qs.FipeCode = v.FipeCode
               AND qs.YearFuel = v.FipeYearFuel
               AND qs.ReferenceMonth = s.Date - INTERVAL (DAYOFMONTH(s.Date) - 1) DAY
               AND qs.IsActive = 1
            WHERE v.IdTenant = @IdTenant
              AND v.IsActive = 1
            ORDER BY v.Status, v.DtCreated DESC
            """;
    }

    /// <summary>
    /// The offers still on the table, against the table of this month.
    ///
    /// Apart from the query above on purpose: a vehicle can carry several open proposals, and
    /// joining them there would multiply every vehicle row by its offers — the classic way a
    /// total silently doubles.
    /// </summary>
    internal sealed class ListMarketProposalsQuery : SqlQuery
    {
        public ListMarketProposalsQuery(int idTenant, DateOnly currentMonth)
        {
            IdTenant = idTenant;
            CurrentMonth = new DateOnly(currentMonth.Year, currentMonth.Month, 1);
        }

        public int IdTenant { get; }

        public DateOnly CurrentMonth { get; }

        public override string GetSql() => """
            SELECT
                v.Code AS VehicleCode,
                v.Plate,
                v.Brand,
                v.Model,
                p.ProspectName,
                p.Amount,
                p.Date,
                qn.Value AS CurrentReference
            FROM Proposal p
            INNER JOIN Vehicle v
                ON v.Id = p.IdVehicle
               AND v.IsActive = 1
            LEFT JOIN FipeQuote qn
                ON qn.FipeCode = v.FipeCode
               AND qn.YearFuel = v.FipeYearFuel
               AND qn.ReferenceMonth = @CurrentMonth
               AND qn.IsActive = 1
            WHERE v.IdTenant = @IdTenant
              AND p.IsActive = 1
              AND p.Status = 1
            ORDER BY p.Date DESC, p.Amount DESC
            """;
    }
}
