using Foundation.Dapper.Sql;

namespace RevendaPro.Infrastructure.Queries.Users
{
    /// <summary>
    /// Columns of User, so every query returns the same shape and Dapper can materialize
    /// the entity, including the audit state inherited from Foundation's Entity.
    /// </summary>
    internal static class UserColumns
    {
        public const string All = """
            Id, Code, TenantId, Name, Email, PasswordHash, Photo, Document, Phone,
            IsActive, DtCreated, CreatedBy, DtUpdated, UpdatedBy, DtDeleted, DeletedBy
            """;

        /// <summary>
        /// Same list, qualified for a query that joins other tables.
        ///
        /// Written out instead of derived from <see cref="All"/> by string replacement:
        /// replacing "Id," would also hit "TenantId," and produce "Tenantu.Id,".
        /// </summary>
        public const string Aliased = """
            u.Id, u.Code, u.TenantId, u.Name, u.Email, u.PasswordHash, u.Photo, u.Document,
            u.Phone, u.IsActive, u.DtCreated, u.CreatedBy, u.DtUpdated, u.UpdatedBy,
            u.DtDeleted, u.DeletedBy
            """;
    }

    /// <summary>Finds an active user by e-mail, for authentication.</summary>
    internal sealed class FindUserByEmailQuery : SqlQuery
    {
        public FindUserByEmailQuery(string email) => Email = email;

        public string Email { get; }

        public override string GetSql() => $"""
            SELECT {UserColumns.All}
            FROM User
            WHERE Email = @Email AND IsActive = 1
            """;
    }

    /// <summary>
    /// Lists the users of a tenant, optionally filtered by name, e-mail or role name.
    /// </summary>
    internal sealed class ListUsersByTenantQuery : SqlQuery
    {
        public ListUsersByTenantQuery(int tenantId, string? search)
        {
            TenantId = tenantId;
            Search = string.IsNullOrWhiteSpace(search) ? null : $"%{search.Trim()}%";
        }

        public int TenantId { get; }

        public string? Search { get; }

        public override string GetSql() => $"""
            SELECT DISTINCT {UserColumns.Aliased}
            FROM User u
            LEFT JOIN UserRole ur ON ur.UserId = u.Id AND ur.IsActive = 1
            LEFT JOIN Role r ON r.Id = ur.RoleId AND r.IsActive = 1
            WHERE u.TenantId = @TenantId
              AND u.IsActive = 1
              AND (@Search IS NULL
                   OR u.Name LIKE @Search
                   OR u.Email LIKE @Search
                   OR r.Name LIKE @Search)
            ORDER BY u.Name
            """;
    }

    /// <summary>Checks whether the e-mail is already taken inside the tenant.</summary>
    internal sealed class UserEmailExistsQuery : SqlQuery
    {
        public UserEmailExistsQuery(int tenantId, string email, int? ignoreId)
        {
            TenantId = tenantId;
            Email = email;
            IgnoreId = ignoreId;
        }

        public int TenantId { get; }

        public string Email { get; }

        public int? IgnoreId { get; }

        public override string GetSql() => """
            SELECT COUNT(1)
            FROM User
            WHERE TenantId = @TenantId
              AND Email = @Email
              AND IsActive = 1
              AND (@IgnoreId IS NULL OR Id <> @IgnoreId)
            """;
    }

    /// <summary>Counts the active users holding a role, to block deleting a role in use.</summary>
    internal sealed class CountUsersByRoleQuery : SqlQuery
    {
        public CountUsersByRoleQuery(int roleId) => RoleId = roleId;

        public int RoleId { get; }

        public override string GetSql() => """
            SELECT COUNT(1)
            FROM UserRole ur
            INNER JOIN User u ON u.Id = ur.UserId AND u.IsActive = 1
            WHERE ur.RoleId = @RoleId AND ur.IsActive = 1
            """;
    }

    /// <summary>
    /// Screen keys the user can reach: the union of the screens of every role they hold.
    ///
    /// Soft deletion is filtered on all four tables. Entity Framework would apply a global
    /// filter; Dapper does not, so a missing IsActive here would hand back a revoked
    /// permission. See ADR-0003.
    /// </summary>
    internal sealed class ListScreenKeysByUserQuery : SqlQuery
    {
        public ListScreenKeysByUserQuery(int userId) => UserId = userId;

        public int UserId { get; }

        public override string GetSql() => """
            SELECT DISTINCT s.`Key`
            FROM UserRole ur
            INNER JOIN Role r ON r.Id = ur.RoleId AND r.IsActive = 1
            INNER JOIN RoleScreen rs ON rs.RoleId = r.Id AND rs.IsActive = 1
            INNER JOIN Screen s ON s.Id = rs.ScreenId AND s.IsActive = 1
            WHERE ur.UserId = @UserId AND ur.IsActive = 1
            ORDER BY s.`Key`
            """;
    }

    /// <summary>Roles currently held by the user.</summary>
    internal sealed class ListRoleIdsByUserQuery : SqlQuery
    {
        public ListRoleIdsByUserQuery(int userId) => UserId = userId;

        public int UserId { get; }

        public override string GetSql() => """
            SELECT RoleId
            FROM UserRole
            WHERE UserId = @UserId AND IsActive = 1
            """;
    }

    /// <summary>Soft deletes every role link of the user, before writing the new set.</summary>
    internal sealed class ClearUserRolesQuery : SqlQuery
    {
        public ClearUserRolesQuery(int userId, string actor)
        {
            UserId = userId;
            Actor = actor;
            DtDeleted = DateTime.UtcNow;
        }

        public int UserId { get; }

        public string Actor { get; }

        public DateTime DtDeleted { get; }

        public override string GetSql() => """
            UPDATE UserRole
            SET IsActive = 0, DtDeleted = @DtDeleted, DeletedBy = @Actor
            WHERE UserId = @UserId AND IsActive = 1
            """;
    }

    /// <summary>
    /// Links a user to a role. Reactivates the previous link when it exists, so the history
    /// of who granted it first is not lost and no duplicate row is created.
    /// </summary>
    internal sealed class GrantRoleToUserQuery : SqlQuery
    {
        public GrantRoleToUserQuery(int userId, int roleId, string actor)
        {
            UserId = userId;
            RoleId = roleId;
            Actor = actor;
            Code = Guid.CreateVersion7();
            DtCreated = DateTime.UtcNow;
        }

        public int UserId { get; }

        public int RoleId { get; }

        public string Actor { get; }

        public Guid Code { get; }

        public DateTime DtCreated { get; }

        public override string GetSql() => """
            INSERT INTO UserRole (Code, UserId, RoleId, IsActive, DtCreated, CreatedBy)
            VALUES (@Code, @UserId, @RoleId, 1, @DtCreated, @Actor)
            ON DUPLICATE KEY UPDATE
                IsActive = 1,
                DtDeleted = NULL,
                DeletedBy = NULL,
                DtUpdated = @DtCreated,
                UpdatedBy = @Actor
            """;
    }
}
