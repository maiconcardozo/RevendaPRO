namespace RevendaPro.Infrastructure.Screens
{
    /// <summary>One screen declared in code. See <see cref="ScreenCatalog"/>.</summary>
    /// <param name="Key">Permission key. Lowercase and stable.</param>
    /// <param name="Name">Menu label. Portuguese on purpose: it is text the user reads.</param>
    /// <param name="Route">Frontend route.</param>
    /// <param name="Icon">Lucide icon name resolved by the frontend.</param>
    /// <param name="MenuGroup">Sidebar section header. Null keeps it out of the menu.</param>
    /// <param name="Order">Position inside the group.</param>
    /// <param name="ShowInMenu">False means a permission without a menu item.</param>
    /// <param name="ParentKey">Parent screen key, for submenus.</param>
    public sealed record DeclaredScreen(
        string Key,
        string Name,
        string Route,
        string? Icon,
        string? MenuGroup,
        int Order,
        bool ShowInMenu = true,
        string? ParentKey = null);

    /// <summary>
    /// Source of truth for WHICH screens exist in the system.
    ///
    /// To add a screen: append a line here and start the API. The
    /// <see cref="ScreenSynchronizer"/> inserts it and grants it to the Administrator role.
    /// No migration and no manual SQL. See ADR-0002.
    ///
    /// Removing a line does NOT delete the screen: it is deactivated and the permission
    /// links are preserved, in case it comes back.
    ///
    /// Key and Route are code, so they are English. Name is the label the user reads, so it
    /// stays in Portuguese. See ADR-0003.
    /// </summary>
    public static class ScreenCatalog
    {
        /// <summary>Sidebar section for the day to day of the dealership.</summary>
        public const string OperationGroup = "Operação";

        /// <summary>Sidebar section for access administration.</summary>
        public const string AdministrationGroup = "Administração";

        /// <summary>Role that automatically receives every new screen.</summary>
        public const string AdministratorRole = "Administrador";

        /// <summary>Every screen the system knows about.</summary>
        public static IReadOnlyList<DeclaredScreen> Screens { get; } =
        [
            new("dashboard", "Dashboard", "/dashboard", "LayoutDashboard", OperationGroup, 1),
            new("vehicles",  "Veículos",  "/vehicles",  "Car",             OperationGroup, 2),
            new("costs",     "Custos",    "/costs",     "Receipt",         OperationGroup, 3),
            new("sales",     "Vendas",    "/sales",     "HandCoins",       OperationGroup, 4),

            new("users", "Usuários", "/users", "Users",       AdministrationGroup, 10),
            new("roles", "Perfis",   "/roles", "ShieldCheck", AdministrationGroup, 11),
            new("expense-types", "Tipos de gasto", "/expense-types", "Tags", AdministrationGroup, 12),

            // Permission without a menu item: reachable by route and enforced by the API,
            // but absent from the sidebar.
            new("my-account", "Meus dados", "/my-account", null, null, 99, ShowInMenu: false)
        ];
    }
}
