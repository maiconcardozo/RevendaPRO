using System.Diagnostics;

namespace RevendaPro.Global.Domain.Entities
{
    /// <summary>
    /// Catalog of permissions AND of the menu. <see cref="Key"/> is the permission.
    ///
    /// Global to the system, not owned by a tenant: the set of screens is the same for
    /// everyone. What varies per tenant is the Role and what it grants. See ADR-0002.
    /// </summary>
    [DebuggerDisplay("Key={Key}, Route={Route}")]
    public class Screen : BaseEntity
    {
        private Screen() { }

        /// <summary>Permission key. Lowercase, stable, referenced by RoleScreen and by the API guard.</summary>
        public string Key { get; private set; } = string.Empty;

        /// <summary>
        /// Menu label. Stays in Portuguese on purpose: it is text the user reads.
        /// Same exception CPComunica registered for Permissions.Name. See ADR-0003.
        /// </summary>
        public string Name { get; private set; } = string.Empty;

        public string Route { get; private set; } = string.Empty;

        /// <summary>Lucide icon name resolved by the frontend.</summary>
        public string? Icon { get; private set; }

        /// <summary>Section header in the sidebar. Null when the screen is not in the menu.</summary>
        public string? MenuGroup { get; private set; }

        public int Order { get; private set; }

        /// <summary>False means a permission without a menu item, still enforced by the API.</summary>
        public bool ShowInMenu { get; private set; }

        public int? ParentScreenId { get; private set; }

        public static Screen Create(
            string key,
            string name,
            string route,
            string? icon,
            string? menuGroup,
            int order,
            bool showInMenu,
            int? parentScreenId = null,
            string createdBy = SystemActor)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Screen key cannot be null or empty.", nameof(key));
            }

            if (string.IsNullOrWhiteSpace(route))
            {
                throw new ArgumentException("Screen route cannot be null or empty.", nameof(route));
            }

            var screen = new Screen
            {
                Key = key.Trim().ToLowerInvariant(),
                Name = name,
                Route = route,
                Icon = icon,
                MenuGroup = menuGroup,
                Order = order,
                ShowInMenu = showInMenu,
                ParentScreenId = parentScreenId
            };

            screen.SetCreatedBy(createdBy);

            return screen;
        }

        /// <summary>
        /// Applies the code-declared catalog over the stored row.
        /// Returns true when something changed, so the synchronizer knows whether to save.
        /// </summary>
        public bool Sync(
            string name,
            string route,
            string? icon,
            string? menuGroup,
            int order,
            bool showInMenu,
            int? parentScreenId,
            string updatedBy = SystemActor)
        {
            var changed = Name != name
                          || Route != route
                          || Icon != icon
                          || MenuGroup != menuGroup
                          || Order != order
                          || ShowInMenu != showInMenu
                          || ParentScreenId != parentScreenId
                          || IsDeleted;

            if (!changed)
            {
                return false;
            }

            Name = name;
            Route = route;
            Icon = icon;
            MenuGroup = menuGroup;
            Order = order;
            ShowInMenu = showInMenu;
            ParentScreenId = parentScreenId;

            if (IsDeleted)
            {
                Activate();
            }

            UpdateAuditInfo(updatedBy);

            return true;
        }
    }
}
