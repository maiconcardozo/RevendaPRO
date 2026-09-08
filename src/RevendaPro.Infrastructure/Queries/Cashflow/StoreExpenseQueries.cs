using Foundation.Dapper.Sql;

namespace RevendaPro.Infrastructure.Queries.Cashflow
{
    /// <summary>
    /// Colunas de StoreExpense, para toda consulta devolver a mesma forma e o Dapper
    /// materializar a entidade, auditoria incluída.
    /// </summary>
    internal static class StoreExpenseColumns
    {
        public const string All = """
            Id, Code, IdTenant, Description, IdExpenseType, IdSupplier, Amount, Date, DueDate,
            PaidDate, IsPaid, Notes, IsActive, DtCreated, CreatedBy, DtUpdated, UpdatedBy,
            DtDeleted, DeletedBy
            """;
    }

    /// <summary>As despesas da loja num período, da mais recente para a mais antiga.</summary>
    internal sealed class ListStoreExpensesQuery(int idTenant, DateOnly? from, DateOnly? to) : SqlQuery
    {
        public int IdTenant { get; } = idTenant;

        public DateOnly? From { get; } = from;

        public DateOnly? To { get; } = to;

        public override string GetSql() => $"""
            SELECT {StoreExpenseColumns.All}
            FROM StoreExpense
            WHERE IdTenant = @IdTenant
              AND IsActive = 1
              AND (@From IS NULL OR Date >= @From)
              AND (@To IS NULL OR Date <= @To)
            ORDER BY Date DESC, Id DESC
            """;
    }

    /// <summary>Uma despesa da loja, pelo código público.</summary>
    internal sealed class FindStoreExpenseByCodeQuery(int idTenant, Guid code) : SqlQuery
    {
        public int IdTenant { get; } = idTenant;

        public Guid Code { get; } = code;

        public override string GetSql() => $"""
            SELECT {StoreExpenseColumns.All}
            FROM StoreExpense
            WHERE Code = @Code
              AND IdTenant = @IdTenant
              AND IsActive = 1
            """;
    }

    /// <summary>Quantas despesas da loja apontam para um tipo de gasto.</summary>
    internal sealed class CountStoreExpensesOfTypeQuery(int idExpenseType) : SqlQuery
    {
        public int IdExpenseType { get; } = idExpenseType;

        public override string GetSql() => """
            SELECT COUNT(1)
            FROM StoreExpense
            WHERE IdExpenseType = @IdExpenseType
              AND IsActive = 1
            """;
    }

    /// <summary>Quantas despesas da loja apontam para um fornecedor.</summary>
    internal sealed class CountStoreExpensesOfSupplierQuery(int idSupplier) : SqlQuery
    {
        public int IdSupplier { get; } = idSupplier;

        public override string GetSql() => """
            SELECT COUNT(1)
            FROM StoreExpense
            WHERE IdSupplier = @IdSupplier
              AND IsActive = 1
            """;
    }
}
