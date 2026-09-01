using Foundation.Dapper.Sql;

namespace RevendaPro.Infrastructure.Queries.Screens
{
    internal static class ScreenColumns
    {
        public const string All = """
            Id, Code, `Key`, Name, Route, Icon, MenuGroup, `Order`, ShowInMenu, ParentScreenId,
            IsActive, DtCreated, CreatedBy, DtUpdated, UpdatedBy, DtDeleted, DeletedBy
            """;
    }

    /// <summary>
    /// Key and Order are reserved words in MySQL, hence the backticks. The property is
    /// still named after the column: no HasColumnName translation layer. See ADR-0003.
    /// </summary>
    internal sealed class FindScreenByKeyQuery(string key) : SqlQuery
    {
        public string Key { get; } = key;

        public override string GetSql() => $"""
            SELECT {ScreenColumns.All}
            FROM Screen
            WHERE `Key` = @Key AND IsActive = 1
            """;
    }

    /// <summary>
    /// Lists every screen, including the inactive ones. The synchronizer needs them all:
    /// a screen that returns to the catalog has to be reactivated, not inserted again.
    /// </summary>
    internal sealed class ListAllScreensQuery : SqlQuery
    {
        public override string GetSql() => $"""
            SELECT {ScreenColumns.All}
            FROM Screen
            ORDER BY MenuGroup, `Order`
            """;
    }

    internal sealed class ListActiveScreensQuery : SqlQuery
    {
        public override string GetSql() => $"""
            SELECT {ScreenColumns.All}
            FROM Screen
            WHERE IsActive = 1
            ORDER BY MenuGroup, `Order`
            """;
    }

    internal sealed class ListScreenKeysByRoleQuery(int roleId) : SqlQuery
    {
        public int RoleId { get; } = roleId;

        public override string GetSql() => """
            SELECT s.`Key`
            FROM RoleScreen rs
            INNER JOIN Screen s ON s.Id = rs.ScreenId AND s.IsActive = 1
            WHERE rs.RoleId = @RoleId AND rs.IsActive = 1
            ORDER BY s.`Key`
            """;
    }
}

namespace RevendaPro.Infrastructure.Queries.Screens
{
    using Foundation.Dapper.Sql;
    using RevendaPro.Domain.Entities;

    /// <summary>
    /// Inserts a screen.
    ///
    /// Written by hand because Key and Order are reserved words in MySQL and need backticks,
    /// which Foundation's conventional statement builder does not add - it stays provider
    /// agnostic, and backticks are MySQL specific.
    /// </summary>
    internal sealed class InsertScreenQuery(Screen screen) : SqlQuery
    {
        public Guid Code { get; } = screen.Code;

        public string Key { get; } = screen.Key;

        public string Name { get; } = screen.Name;

        public string Route { get; } = screen.Route;

        public string? Icon { get; } = screen.Icon;

        public string? MenuGroup { get; } = screen.MenuGroup;

        public int Order { get; } = screen.Order;

        public bool ShowInMenu { get; } = screen.ShowInMenu;

        public int? ParentScreenId { get; } = screen.ParentScreenId;

        public bool IsActive { get; } = screen.IsActive;

        public DateTime DtCreated { get; } = screen.DtCreated;

        public string CreatedBy { get; } = screen.CreatedBy;

        public override string GetSql() => """
            INSERT INTO Screen
                (Code, `Key`, Name, Route, Icon, MenuGroup, `Order`, ShowInMenu,
                 ParentScreenId, IsActive, DtCreated, CreatedBy)
            VALUES
                (@Code, @Key, @Name, @Route, @Icon, @MenuGroup, @Order, @ShowInMenu,
                 @ParentScreenId, @IsActive, @DtCreated, @CreatedBy)
            """;
    }

    /// <summary>Updates a screen. Same reserved word reason as the insert.</summary>
    internal sealed class UpdateScreenQuery(Screen screen) : SqlQuery
    {
        public int Id { get; } = screen.Id;

        public string Name { get; } = screen.Name;

        public string Route { get; } = screen.Route;

        public string? Icon { get; } = screen.Icon;

        public string? MenuGroup { get; } = screen.MenuGroup;

        public int Order { get; } = screen.Order;

        public bool ShowInMenu { get; } = screen.ShowInMenu;

        public int? ParentScreenId { get; } = screen.ParentScreenId;

        public bool IsActive { get; } = screen.IsActive;

        public DateTime? DtUpdated { get; } = screen.DtUpdated;

        public string? UpdatedBy { get; } = screen.UpdatedBy;

        public DateTime? DtDeleted { get; } = screen.DtDeleted;

        public string? DeletedBy { get; } = screen.DeletedBy;

        public override string GetSql() => """
            UPDATE Screen SET
                Name = @Name,
                Route = @Route,
                Icon = @Icon,
                MenuGroup = @MenuGroup,
                `Order` = @Order,
                ShowInMenu = @ShowInMenu,
                ParentScreenId = @ParentScreenId,
                IsActive = @IsActive,
                DtUpdated = @DtUpdated,
                UpdatedBy = @UpdatedBy,
                DtDeleted = @DtDeleted,
                DeletedBy = @DeletedBy
            WHERE Id = @Id
            """;
    }
}
