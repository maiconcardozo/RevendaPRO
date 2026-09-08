using Foundation.Dapper.Sql;

namespace RevendaPro.Infrastructure.Queries.Cashflow
{
    /// <summary>Colunas de SaleReceipt, para toda consulta materializar a entidade inteira.</summary>
    internal static class SaleReceiptColumns
    {
        public const string All = """
            Id, Code, IdSale, Amount, Date, PaymentMethod, Notes,
            IsActive, DtCreated, CreatedBy, DtUpdated, UpdatedBy, DtDeleted, DeletedBy
            """;

        /// <summary>A mesma lista, qualificada. Escrita por extenso: texto jamais enxerga gramática.</summary>
        public const string Aliased = """
            r.Id, r.Code, r.IdSale, r.Amount, r.Date, r.PaymentMethod, r.Notes,
            r.IsActive, r.DtCreated, r.CreatedBy, r.DtUpdated, r.UpdatedBy, r.DtDeleted, r.DeletedBy
            """;
    }

    /// <summary>As entradas de uma venda, da mais antiga para a mais nova.</summary>
    internal sealed class ListReceiptsOfSaleQuery(int idSale) : SqlQuery
    {
        public int IdSale { get; } = idSale;

        public override string GetSql() => $"""
            SELECT {SaleReceiptColumns.All}
            FROM SaleReceipt
            WHERE IdSale = @IdSale
              AND IsActive = 1
            ORDER BY Date, Id
            """;
    }

    /// <summary>As entradas de várias vendas de uma vez.</summary>
    internal sealed class ListReceiptsOfSalesQuery(IReadOnlyCollection<int> idSales) : SqlQuery
    {
        public IReadOnlyCollection<int> IdSales { get; } = idSales;

        public override string GetSql() => $"""
            SELECT {SaleReceiptColumns.All}
            FROM SaleReceipt
            WHERE IdSale IN @IdSales
              AND IsActive = 1
            ORDER BY Date, Id
            """;
    }

    /// <summary>
    /// Uma entrada pelo código público, dentro da revenda.
    ///
    /// O isolamento chega em dois saltos: a entrada pertence à venda, a venda ao carro, e o
    /// carro à revenda. É onde o filtro do <c>IdTenant</c> mora, e por isso os dois JOIN.
    /// </summary>
    internal sealed class FindReceiptByCodeQuery(int idTenant, Guid code) : SqlQuery
    {
        public int IdTenant { get; } = idTenant;

        public Guid Code { get; } = code;

        public override string GetSql() => $"""
            SELECT {SaleReceiptColumns.Aliased}
            FROM SaleReceipt r
            INNER JOIN Sale s ON s.Id = r.IdSale AND s.IsActive = 1
            INNER JOIN Vehicle v ON v.Id = s.IdVehicle AND v.IsActive = 1
            WHERE r.Code = @Code
              AND v.IdTenant = @IdTenant
              AND r.IsActive = 1
            """;
    }
}
