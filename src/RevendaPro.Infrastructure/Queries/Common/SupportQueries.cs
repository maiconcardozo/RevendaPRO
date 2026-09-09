using Foundation.Dapper.Sql;

namespace RevendaPro.Infrastructure.Queries.Common
{
    /// <summary>Colunas de Tenant, para toda consulta materializar a entidade inteira.</summary>
    internal static class TenantColumns
    {
        public const string All = """
            Id, Code, Name, Document, Phone, Email, Address, Logo,
            IsActive, DtCreated, CreatedBy, DtUpdated, UpdatedBy, DtDeleted, DeletedBy
            """;
    }

    internal sealed class FindFirstTenantQuery : SqlQuery
    {
        public override string GetSql() => $"""
            SELECT {TenantColumns.All}
            FROM Tenant
            WHERE IsActive = 1
            ORDER BY Id
            LIMIT 1
            """;
    }

    /// <summary>Todas as revendas ativas, por Id, para as rotinas de subida.</summary>
    internal sealed class ListAllTenantsQuery : SqlQuery
    {
        public override string GetSql() => $"""
            SELECT {TenantColumns.All}
            FROM Tenant
            WHERE IsActive = 1
            ORDER BY Id
            """;
    }

    /// <summary>A revenda de quem está logado, pelo Id do token.</summary>
    internal sealed class FindTenantByIdQuery(int id) : SqlQuery
    {
        public int Id { get; } = id;

        public override string GetSql() => $"""
            SELECT {TenantColumns.All}
            FROM Tenant
            WHERE Id = @Id
              AND IsActive = 1
            """;
    }

    internal sealed class FindRefreshTokenByHashQuery(string tokenHash) : SqlQuery
    {
        public string TokenHash { get; } = tokenHash;

        public override string GetSql() => """
            SELECT Id, Code, IdUser, TokenHash, ExpiresAt, RevokedAt,
                   IsActive, DtCreated, CreatedBy, DtUpdated, UpdatedBy, DtDeleted, DeletedBy
            FROM RefreshToken
            WHERE TokenHash = @TokenHash AND IsActive = 1
            """;
    }

    internal sealed class RevokeUserRefreshTokensQuery(int idUser, string actor) : SqlQuery
    {
        public int IdUser { get; } = idUser;

        public string Actor { get; } = actor;

        public DateTime RevokedAt { get; } = DateTime.UtcNow;

        public override string GetSql() => """
            UPDATE RefreshToken
            SET RevokedAt = @RevokedAt, DtUpdated = @RevokedAt, UpdatedBy = @Actor
            WHERE IdUser = @IdUser AND RevokedAt IS NULL AND IsActive = 1
            """;
    }
}
