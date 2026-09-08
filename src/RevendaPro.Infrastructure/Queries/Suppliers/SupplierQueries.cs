using Foundation.Dapper.Sql;

namespace RevendaPro.Infrastructure.Queries.Suppliers
{
    /// <summary>
    /// Colunas de Supplier, para toda consulta devolver a mesma forma e o Dapper materializar a
    /// entidade, auditoria incluída.
    /// </summary>
    internal static class SupplierColumns
    {
        public const string All = """
            Id, Code, IdTenant, Name, IdSupplierSegment, ContactName, ContactPhone, Document,
            Notes, IsActive, DtCreated, CreatedBy, DtUpdated, UpdatedBy, DtDeleted, DeletedBy
            """;
    }

    /// <summary>Os fornecedores de uma revenda, por nome.</summary>
    internal sealed class ListSuppliersByTenantQuery(int idTenant) : SqlQuery
    {
        public int IdTenant { get; } = idTenant;

        public override string GetSql() => $"""
            SELECT {SupplierColumns.All}
            FROM Supplier
            WHERE IdTenant = @IdTenant
              AND IsActive = 1
            ORDER BY Name
            """;
    }

    /// <summary>Um fornecedor de uma revenda, pelo código público.</summary>
    internal sealed class FindSupplierByCodeQuery(int idTenant, Guid code) : SqlQuery
    {
        public int IdTenant { get; } = idTenant;

        public Guid Code { get; } = code;

        public override string GetSql() => $"""
            SELECT {SupplierColumns.All}
            FROM Supplier
            WHERE Code = @Code
              AND IdTenant = @IdTenant
              AND IsActive = 1
            """;
    }

    /// <summary>Quantos gastos apontam para um fornecedor.</summary>
    internal sealed class CountExpensesOfSupplierQuery(int idSupplier) : SqlQuery
    {
        public int IdSupplier { get; } = idSupplier;

        public override string GetSql() => """
            SELECT COUNT(1)
            FROM VehicleExpense
            WHERE IdSupplier = @IdSupplier
              AND IsActive = 1
            """;
    }

    /// <summary>
    /// Se a revenda já tem um fornecedor com esse nome.
    ///
    /// Conferido aqui, e não por índice único, pelo mesmo motivo do pátio: o fornecedor
    /// excluído mantém a linha, e um índice recusaria um nome que voltou a ser usado.
    /// </summary>
    internal sealed class SupplierNameExistsQuery(int idTenant, string name, int? ignoreId) : SqlQuery
    {
        public int IdTenant { get; } = idTenant;

        public string Name { get; } = name;

        public int? IgnoreId { get; } = ignoreId;

        public override string GetSql() => """
            SELECT COUNT(1)
            FROM Supplier
            WHERE IdTenant = @IdTenant
              AND Name = @Name
              AND IsActive = 1
              AND (@IgnoreId IS NULL OR Id <> @IgnoreId)
            """;
    }

    /// <summary>
    /// Quanto foi para cada fornecedor da revenda, somado pelo banco.
    ///
    /// Só o pago entra no total que a tela chama de "quanto gastei"; o previsto vem numa coluna
    /// própria (RF-11). O período é lido sobre a data do gasto, e os dois limites são opcionais:
    /// a tela Fornecedores abre em "desde o início", e o painel manda o mês.
    /// </summary>
    internal sealed class SumExpensesBySupplierQuery(int idTenant, DateOnly? from, DateOnly? to) : SqlQuery
    {
        public int IdTenant { get; } = idTenant;

        public DateOnly? From { get; } = from;

        public DateOnly? To { get; } = to;

        public override string GetSql() => """
            SELECT e.IdSupplier,
                   COALESCE(SUM(CASE WHEN e.IsPaid = 1 THEN e.Amount ELSE 0 END), 0) AS PaidTotal,
                   COALESCE(SUM(CASE WHEN e.IsPaid = 0 THEN e.Amount ELSE 0 END), 0) AS PlannedTotal,
                   COUNT(1) AS ExpenseCount,
                   MAX(e.Date) AS LastDate
            FROM VehicleExpense e
            INNER JOIN Vehicle v ON v.Id = e.IdVehicle AND v.IsActive = 1
            WHERE v.IdTenant = @IdTenant
              AND e.IsActive = 1
              AND e.IdSupplier IS NOT NULL
              AND (@From IS NULL OR e.Date >= @From)
              AND (@To IS NULL OR e.Date <= @To)
            GROUP BY e.IdSupplier
            ORDER BY PaidTotal DESC, ExpenseCount DESC
            """;
    }

    /// <summary>Os gastos de um fornecedor, com o carro de cada um.</summary>
    internal sealed class ListExpensesOfSupplierQuery(
        int idTenant,
        int idSupplier,
        DateOnly? from,
        DateOnly? to) : SqlQuery
    {
        public int IdTenant { get; } = idTenant;

        public int IdSupplier { get; } = idSupplier;

        public DateOnly? From { get; } = from;

        public DateOnly? To { get; } = to;

        public override string GetSql() => """
            SELECT e.Code, e.Date, e.Description, e.Amount, e.IsPaid, e.IdExpenseType,
                   v.Code AS VehicleCode, v.Plate, v.Brand, v.Model, v.Version, v.ModelYear
            FROM VehicleExpense e
            INNER JOIN Vehicle v ON v.Id = e.IdVehicle AND v.IsActive = 1
            WHERE v.IdTenant = @IdTenant
              AND e.IdSupplier = @IdSupplier
              AND e.IsActive = 1
              AND (@From IS NULL OR e.Date >= @From)
              AND (@To IS NULL OR e.Date <= @To)
            ORDER BY e.Date DESC, e.Id DESC
            """;
    }

    /// <summary>
    /// O gasto com fornecedores num período, somado de uma forma: por ramo, por tipo de gasto ou
    /// por mês. Uma classe por forma, e o mesmo miolo: só gasto ativo, de carro ativo desta
    /// revenda, com fornecedor, na janela de datas.
    /// </summary>
    internal abstract class SumSupplierSpendQuery(int idTenant, DateOnly? from, DateOnly? to) : SqlQuery
    {
        public int IdTenant { get; } = idTenant;

        public DateOnly? From { get; } = from;

        public DateOnly? To { get; } = to;

        /// <summary>A expressão que agrupa, e os joins que ela precisa.</summary>
        protected abstract string Key { get; }

        protected virtual string Joins => string.Empty;

        public override string GetSql() => $"""
            SELECT {Key} AS `Key`,
                   COALESCE(SUM(CASE WHEN e.IsPaid = 1 THEN e.Amount ELSE 0 END), 0) AS PaidTotal,
                   COALESCE(SUM(CASE WHEN e.IsPaid = 0 THEN e.Amount ELSE 0 END), 0) AS PlannedTotal,
                   COUNT(1) AS ExpenseCount
            FROM VehicleExpense e
            INNER JOIN Vehicle v ON v.Id = e.IdVehicle AND v.IsActive = 1
            {Joins}
            WHERE v.IdTenant = @IdTenant
              AND e.IsActive = 1
              AND e.IdSupplier IS NOT NULL
              AND (@From IS NULL OR e.Date >= @From)
              AND (@To IS NULL OR e.Date <= @To)
            GROUP BY {Key}
            ORDER BY PaidTotal DESC
            """;
    }

    /// <summary>O gasto com fornecedores por ramo.</summary>
    internal sealed class SumSpendBySegmentQuery(int idTenant, DateOnly? from, DateOnly? to)
        : SumSupplierSpendQuery(idTenant, from, to)
    {
        protected override string Key => "s.IdSupplierSegment";

        protected override string Joins => "INNER JOIN Supplier s ON s.Id = e.IdSupplier AND s.IsActive = 1";
    }

    /// <summary>O gasto com fornecedores por tipo de gasto.</summary>
    internal sealed class SumSpendByTypeQuery(int idTenant, DateOnly? from, DateOnly? to)
        : SumSupplierSpendQuery(idTenant, from, to)
    {
        protected override string Key => "e.IdExpenseType";
    }

    /// <summary>O gasto com fornecedores por mês, com a chave ano × 100 + mês.</summary>
    internal sealed class SumSpendByMonthQuery(int idTenant, DateOnly? from, DateOnly? to)
        : SumSupplierSpendQuery(idTenant, from, to)
    {
        protected override string Key => "(YEAR(e.Date) * 100 + MONTH(e.Date))";
    }

    /// <summary>
    /// Os totais do gasto com fornecedores num período, e o que ficou sem fornecedor ao lado —
    /// para o painel dizer quanto do dinheiro ainda está sem nome.
    /// </summary>
    internal sealed class SumSupplierTotalsQuery(int idTenant, DateOnly? from, DateOnly? to) : SqlQuery
    {
        public int IdTenant { get; } = idTenant;

        public DateOnly? From { get; } = from;

        public DateOnly? To { get; } = to;

        public override string GetSql() => """
            SELECT COALESCE(SUM(CASE WHEN e.IdSupplier IS NOT NULL AND e.IsPaid = 1 THEN e.Amount ELSE 0 END), 0) AS PaidTotal,
                   COALESCE(SUM(CASE WHEN e.IdSupplier IS NOT NULL AND e.IsPaid = 0 THEN e.Amount ELSE 0 END), 0) AS PlannedTotal,
                   COUNT(CASE WHEN e.IdSupplier IS NOT NULL THEN 1 END) AS ExpenseCount,
                   COUNT(DISTINCT CASE WHEN e.IdSupplier IS NOT NULL THEN e.IdVehicle END) AS VehicleCount,
                   COALESCE(SUM(CASE WHEN e.IdSupplier IS NULL AND e.IsPaid = 1 THEN e.Amount ELSE 0 END), 0) AS UnassignedPaid
            FROM VehicleExpense e
            INNER JOIN Vehicle v ON v.Id = e.IdVehicle AND v.IsActive = 1
            WHERE v.IdTenant = @IdTenant
              AND e.IsActive = 1
              AND (@From IS NULL OR e.Date >= @From)
              AND (@To IS NULL OR e.Date <= @To)
            """;
    }

    /// <summary>Colunas de SupplierSegment.</summary>
    internal static class SupplierSegmentColumns
    {
        public const string All = """
            Id, Code, IdTenant, Name, Position, IsActive, DtCreated, CreatedBy, DtUpdated,
            UpdatedBy, DtDeleted, DeletedBy
            """;
    }

    /// <summary>Os ramos de uma revenda, na ordem em que ela os mostra.</summary>
    internal sealed class ListSupplierSegmentsByTenantQuery(int idTenant) : SqlQuery
    {
        public int IdTenant { get; } = idTenant;

        public override string GetSql() => $"""
            SELECT {SupplierSegmentColumns.All}
            FROM SupplierSegment
            WHERE IdTenant = @IdTenant
              AND IsActive = 1
            ORDER BY Position, Name
            """;
    }

    /// <summary>Um ramo de uma revenda, pelo código público.</summary>
    internal sealed class FindSupplierSegmentByCodeQuery(int idTenant, Guid code) : SqlQuery
    {
        public int IdTenant { get; } = idTenant;

        public Guid Code { get; } = code;

        public override string GetSql() => $"""
            SELECT {SupplierSegmentColumns.All}
            FROM SupplierSegment
            WHERE Code = @Code
              AND IdTenant = @IdTenant
              AND IsActive = 1
            """;
    }

    /// <summary>Quantos fornecedores estão num ramo.</summary>
    internal sealed class CountSuppliersInSegmentQuery(int idSupplierSegment) : SqlQuery
    {
        public int IdSupplierSegment { get; } = idSupplierSegment;

        public override string GetSql() => """
            SELECT COUNT(1)
            FROM Supplier
            WHERE IdSupplierSegment = @IdSupplierSegment
              AND IsActive = 1
            """;
    }

    /// <summary>Se a revenda já tem um ramo com esse nome.</summary>
    internal sealed class SupplierSegmentNameExistsQuery(int idTenant, string name, int? ignoreId) : SqlQuery
    {
        public int IdTenant { get; } = idTenant;

        public string Name { get; } = name;

        public int? IgnoreId { get; } = ignoreId;

        public override string GetSql() => """
            SELECT COUNT(1)
            FROM SupplierSegment
            WHERE IdTenant = @IdTenant
              AND Name = @Name
              AND IsActive = 1
              AND (@IgnoreId IS NULL OR Id <> @IgnoreId)
            """;
    }
}
