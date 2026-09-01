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
            Id, Code, IdTenant, Name, Email, PasswordHash, Photo, Document, Phone,
            IsActive, DtCreated, CreatedBy, DtUpdated, UpdatedBy, DtDeleted, DeletedBy
            """;

        /// <summary>
        /// Same list, qualified for a query that joins other tables.
        ///
        /// Written out instead of derived from <see cref="All"/> by string replacement:
        /// replacing "Id," would also hit "IdTenant," and produce "Tenantu.Id,".
        /// </summary>
        public const string Aliased = """
            u.Id, u.Code, u.IdTenant, u.Name, u.Email, u.PasswordHash, u.Photo, u.Document,
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
        public ListUsersByTenantQuery(int idTenant, string? search)
        {
            IdTenant = idTenant;
            Search = string.IsNullOrWhiteSpace(search) ? null : $"%{search.Trim()}%";
        }

        public int IdTenant { get; }

        public string? Search { get; }

        public override string GetSql() => $"""
            SELECT DISTINCT {UserColumns.Aliased}
            FROM User u
            LEFT JOIN UserRole ur ON ur.IdUser = u.Id AND ur.IsActive = 1
            LEFT JOIN Role r ON r.Id = ur.IdRole AND r.IsActive = 1
            WHERE u.IdTenant = @IdTenant
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
        public UserEmailExistsQuery(int idTenant, string email, int? ignoreId)
        {
            IdTenant = idTenant;
            Email = email;
            IgnoreId = ignoreId;
        }

        public int IdTenant { get; }

        public string Email { get; }

        public int? IgnoreId { get; }

        public override string GetSql() => """
            SELECT COUNT(1)
            FROM User
            WHERE IdTenant = @IdTenant
              AND Email = @Email
              AND IsActive = 1
              AND (@IgnoreId IS NULL OR Id <> @IgnoreId)
            """;
    }

    /// <summary>Counts the active users holding a role, to block deleting a role in use.</summary>
    internal sealed class CountUsersByRoleQuery : SqlQuery
    {
        public CountUsersByRoleQuery(int idRole) => IdRole = idRole;

        public int IdRole { get; }

        public override string GetSql() => """
            SELECT COUNT(1)
            FROM UserRole ur
            INNER JOIN User u ON u.Id = ur.IdUser AND u.IsActive = 1
            WHERE ur.IdRole = @IdRole AND ur.IsActive = 1
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
        public ListScreenKeysByUserQuery(int idUser) => IdUser = idUser;

        public int IdUser { get; }

        public override string GetSql() => """
            SELECT DISTINCT s.`Key`
            FROM UserRole ur
            INNER JOIN Role r ON r.Id = ur.IdRole AND r.IsActive = 1
            INNER JOIN RoleScreen rs ON rs.IdRole = r.Id AND rs.IsActive = 1
            INNER JOIN Screen s ON s.Id = rs.IdScreen AND s.IsActive = 1
            WHERE ur.IdUser = @IdUser AND ur.IsActive = 1
            ORDER BY s.`Key`
            """;
    }

    /// <summary>Roles currently held by the user.</summary>
    internal sealed class ListRoleIdsByUserQuery : SqlQuery
    {
        public ListRoleIdsByUserQuery(int idUser) => IdUser = idUser;

        public int IdUser { get; }

        public override string GetSql() => """
            SELECT IdRole
            FROM UserRole
            WHERE IdUser = @IdUser AND IsActive = 1
            """;
    }

    /// <summary>Soft deletes every role link of the user, before writing the new set.</summary>
    internal sealed class ClearUserRolesQuery : SqlQuery
    {
        public ClearUserRolesQuery(int idUser, string actor)
        {
            IdUser = idUser;
            Actor = actor;
            DtDeleted = DateTime.UtcNow;
        }

        public int IdUser { get; }

        public string Actor { get; }

        public DateTime DtDeleted { get; }

        public override string GetSql() => """
            UPDATE UserRole
            SET IsActive = 0, DtDeleted = @DtDeleted, DeletedBy = @Actor
            WHERE IdUser = @IdUser AND IsActive = 1
            """;
    }

    /// <summary>
    /// Links a user to a role. Reactivates the previous link when it exists, so the history
    /// of who granted it first is not lost and no duplicate row is created.
    /// </summary>
    internal sealed class GrantRoleToUserQuery : SqlQuery
    {
        public GrantRoleToUserQuery(int idUser, int idRole, string actor)
        {
            IdUser = idUser;
            IdRole = idRole;
            Actor = actor;
            Code = Guid.CreateVersion7();
            DtCreated = DateTime.UtcNow;
        }

        public int IdUser { get; }

        public int IdRole { get; }

        public string Actor { get; }

        public Guid Code { get; }

        public DateTime DtCreated { get; }

        public override string GetSql() => """
            INSERT INTO UserRole (Code, IdUser, IdRole, IsActive, DtCreated, CreatedBy)
            VALUES (@Code, @IdUser, @IdRole, 1, @DtCreated, @Actor)
            ON DUPLICATE KEY UPDATE
                IsActive = 1,
                DtDeleted = NULL,
                DeletedBy = NULL,
                DtUpdated = @DtCreated,
                UpdatedBy = @Actor
            """;
    }
}
