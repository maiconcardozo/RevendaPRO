using Foundation.Dapper.Sql;

namespace RevendaPro.Infrastructure.Queries.Roles
{
    internal static class RoleColumns
    {
        public const string All = """
            Id, Code, TenantId, Name, Description, IsSystem,
            IsActive, DtCreated, CreatedBy, DtUpdated, UpdatedBy, DtDeleted, DeletedBy
            """;
    }

    internal sealed class FindRoleByNameQuery(int tenantId, string name) : SqlQuery
    {
        public int TenantId { get; } = tenantId;

        public string Name { get; } = name;

        public override string GetSql() => $"""
            SELECT {RoleColumns.All}
            FROM Role
            WHERE TenantId = @TenantId AND Name = @Name AND IsActive = 1
            """;
    }

    internal sealed class ListRolesByTenantQuery(int tenantId) : SqlQuery
    {
        public int TenantId { get; } = tenantId;

        public override string GetSql() => $"""
            SELECT {RoleColumns.All}
            FROM Role
            WHERE TenantId = @TenantId AND IsActive = 1
            ORDER BY Name
            """;
    }

    internal sealed class RoleNameExistsQuery(int tenantId, string name, int? ignoreId) : SqlQuery
    {
        public int TenantId { get; } = tenantId;

        public string Name { get; } = name;

        public int? IgnoreId { get; } = ignoreId;

        public override string GetSql() => """
            SELECT COUNT(1)
            FROM Role
            WHERE TenantId = @TenantId
              AND Name = @Name
              AND IsActive = 1
              AND (@IgnoreId IS NULL OR Id <> @IgnoreId)
            """;
    }

    internal sealed class ListScreenIdsByRoleQuery(int roleId) : SqlQuery
    {
        public int RoleId { get; } = roleId;

        public override string GetSql() => """
            SELECT ScreenId
            FROM RoleScreen
            WHERE RoleId = @RoleId AND IsActive = 1
            """;
    }

    internal sealed class ClearRoleScreensQuery(int roleId, string actor) : SqlQuery
    {
        public int RoleId { get; } = roleId;

        public string Actor { get; } = actor;

        public DateTime DtDeleted { get; } = DateTime.UtcNow;

        public override string GetSql() => """
            UPDATE RoleScreen
            SET IsActive = 0, DtDeleted = @DtDeleted, DeletedBy = @Actor
            WHERE RoleId = @RoleId AND IsActive = 1
            """;
    }

    /// <summary>
    /// Grants one screen to a role. Reactivates the previous link when it exists, so no
    /// duplicate row is created and the original grant date survives.
    /// </summary>
    internal sealed class GrantScreenToRoleQuery(int roleId, int screenId, string actor) : SqlQuery
    {
        public int RoleId { get; } = roleId;

        public int ScreenId { get; } = screenId;

        public string Actor { get; } = actor;

        public Guid Code { get; } = Guid.CreateVersion7();

        public DateTime DtCreated { get; } = DateTime.UtcNow;

        public override string GetSql() => """
            INSERT INTO RoleScreen (Code, RoleId, ScreenId, IsActive, DtCreated, CreatedBy)
            VALUES (@Code, @RoleId, @ScreenId, 1, @DtCreated, @Actor)
            ON DUPLICATE KEY UPDATE
                IsActive = 1,
                DtDeleted = NULL,
                DeletedBy = NULL,
                DtUpdated = @DtCreated,
                UpdatedBy = @Actor
            """;
    }
}
