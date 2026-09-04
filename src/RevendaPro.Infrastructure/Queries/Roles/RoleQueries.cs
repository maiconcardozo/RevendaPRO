using Foundation.Dapper.Sql;

namespace RevendaPro.Infrastructure.Queries.Roles
{
    internal static class RoleColumns
    {
        public const string All = """
            Id, Code, IdTenant, Name, Description, IsSystem,
            IsActive, DtCreated, CreatedBy, DtUpdated, UpdatedBy, DtDeleted, DeletedBy
            """;
    }

    /// <summary>
    /// Finds an active role of a dealership by public code.
    ///
    /// Same reason as the user: a role reached without the tenant is a role of somebody
    /// else, and granting screens on it opens another dealership's system (RNF-04).
    /// </summary>
    internal sealed class FindRoleByCodeQuery(int idTenant, Guid code) : SqlQuery
    {
        public int IdTenant { get; } = idTenant;

        public Guid Code { get; } = code;

        public override string GetSql() => $"""
            SELECT {RoleColumns.All}
            FROM Role
            WHERE Code = @Code AND IdTenant = @IdTenant AND IsActive = 1
            """;
    }

    internal sealed class FindRoleByNameQuery(int idTenant, string name) : SqlQuery
    {
        public int IdTenant { get; } = idTenant;

        public string Name { get; } = name;

        public override string GetSql() => $"""
            SELECT {RoleColumns.All}
            FROM Role
            WHERE IdTenant = @IdTenant AND Name = @Name AND IsActive = 1
            """;
    }

    internal sealed class ListRolesByTenantQuery(int idTenant) : SqlQuery
    {
        public int IdTenant { get; } = idTenant;

        public override string GetSql() => $"""
            SELECT {RoleColumns.All}
            FROM Role
            WHERE IdTenant = @IdTenant AND IsActive = 1
            ORDER BY Name
            """;
    }

    internal sealed class RoleNameExistsQuery(int idTenant, string name, int? ignoreId) : SqlQuery
    {
        public int IdTenant { get; } = idTenant;

        public string Name { get; } = name;

        public int? IgnoreId { get; } = ignoreId;

        public override string GetSql() => """
            SELECT COUNT(1)
            FROM Role
            WHERE IdTenant = @IdTenant
              AND Name = @Name
              AND IsActive = 1
              AND (@IgnoreId IS NULL OR Id <> @IgnoreId)
            """;
    }

    internal sealed class ListScreenIdsByRoleQuery(int idRole) : SqlQuery
    {
        public int IdRole { get; } = idRole;

        public override string GetSql() => """
            SELECT IdScreen
            FROM RoleScreen
            WHERE IdRole = @IdRole AND IsActive = 1
            """;
    }

    internal sealed class ClearRoleScreensQuery(int idRole, string actor) : SqlQuery
    {
        public int IdRole { get; } = idRole;

        public string Actor { get; } = actor;

        public DateTime DtDeleted { get; } = DateTime.UtcNow;

        public override string GetSql() => """
            UPDATE RoleScreen
            SET IsActive = 0, DtDeleted = @DtDeleted, DeletedBy = @Actor
            WHERE IdRole = @IdRole AND IsActive = 1
            """;
    }

    /// <summary>
    /// Grants one screen to a role. Reactivates the previous link when it exists, so no
    /// duplicate row is created and the original grant date survives.
    /// </summary>
    internal sealed class GrantScreenToRoleQuery(int idRole, int idScreen, string actor) : SqlQuery
    {
        public int IdRole { get; } = idRole;

        public int IdScreen { get; } = idScreen;

        public string Actor { get; } = actor;

        public Guid Code { get; } = Guid.CreateVersion7();

        public DateTime DtCreated { get; } = DateTime.UtcNow;

        public override string GetSql() => """
            INSERT INTO RoleScreen (Code, IdRole, IdScreen, IsActive, DtCreated, CreatedBy)
            VALUES (@Code, @IdRole, @IdScreen, 1, @DtCreated, @Actor)
            ON DUPLICATE KEY UPDATE
                IsActive = 1,
                DtDeleted = NULL,
                DeletedBy = NULL,
                DtUpdated = @DtCreated,
                UpdatedBy = @Actor
            """;
    }
}
