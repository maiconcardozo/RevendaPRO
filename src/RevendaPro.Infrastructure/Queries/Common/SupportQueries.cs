using Foundation.Dapper.Sql;

namespace RevendaPro.Infrastructure.Queries.Common
{
    internal sealed class FindFirstTenantQuery : SqlQuery
    {
        public override string GetSql() => """
            SELECT Id, Code, Name,
                   IsActive, DtCreated, CreatedBy, DtUpdated, UpdatedBy, DtDeleted, DeletedBy
            FROM Tenant
            WHERE IsActive = 1
            ORDER BY Id
            LIMIT 1
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
