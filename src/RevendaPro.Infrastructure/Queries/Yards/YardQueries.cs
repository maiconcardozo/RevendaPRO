using Foundation.Dapper.Sql;

namespace RevendaPro.Infrastructure.Queries.Yards
{
    /// <summary>
    /// Colunas de Yard, para toda consulta devolver a mesma forma e o Dapper materializar a
    /// entidade, auditoria incluída.
    /// </summary>
    internal static class YardColumns
    {
        public const string All = """
            Id, Code, IdTenant, Name, Kind, ContactName, ContactPhone, CutPercent, CutAmount,
            Notes, Position, IsActive, DtCreated, CreatedBy, DtUpdated, UpdatedBy, DtDeleted,
            DeletedBy
            """;
    }

    /// <summary>Os pátios de uma revenda, na ordem em que ela os mostra.</summary>
    internal sealed class ListYardsByTenantQuery(int idTenant) : SqlQuery
    {
        public int IdTenant { get; } = idTenant;

        public override string GetSql() => $"""
            SELECT {YardColumns.All}
            FROM Yard
            WHERE IdTenant = @IdTenant
              AND IsActive = 1
            ORDER BY Position, Name
            """;
    }

    /// <summary>Um pátio de uma revenda, pelo código público.</summary>
    internal sealed class FindYardByCodeQuery(int idTenant, Guid code) : SqlQuery
    {
        public int IdTenant { get; } = idTenant;

        public Guid Code { get; } = code;

        public override string GetSql() => $"""
            SELECT {YardColumns.All}
            FROM Yard
            WHERE Code = @Code
              AND IdTenant = @IdTenant
              AND IsActive = 1
            """;
    }

    /// <summary>Quantos carros estão num pátio.</summary>
    internal sealed class CountVehiclesInYardQuery(int idYard) : SqlQuery
    {
        public int IdYard { get; } = idYard;

        public override string GetSql() => """
            SELECT COUNT(1)
            FROM Vehicle
            WHERE IdYard = @IdYard
              AND IsActive = 1
            """;
    }

    /// <summary>
    /// Se a revenda já tem um pátio com esse nome.
    ///
    /// A unicidade é conferida aqui, e não por índice único, pelo mesmo motivo da placa: o pátio
    /// excluído mantém a linha, e um índice recusaria um nome que voltou a ser usado.
    /// </summary>
    internal sealed class YardNameExistsQuery(int idTenant, string name, int? ignoreId) : SqlQuery
    {
        public int IdTenant { get; } = idTenant;

        public string Name { get; } = name;

        public int? IgnoreId { get; } = ignoreId;

        public override string GetSql() => """
            SELECT COUNT(1)
            FROM Yard
            WHERE IdTenant = @IdTenant
              AND Name = @Name
              AND IsActive = 1
              AND (@IgnoreId IS NULL OR Id <> @IgnoreId)
            """;
    }
}
