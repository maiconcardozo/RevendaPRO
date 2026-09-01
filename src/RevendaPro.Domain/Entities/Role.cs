using Foundation.Domain.Abstractions;
using System.Diagnostics;

namespace RevendaPro.Domain.Entities
{
    /// <summary>Access profile. What it grants lives in <see cref="RoleScreen"/>.</summary>
    [DebuggerDisplay("Name={Name}, IdTenant={IdTenant}")]
    public class Role : TenantEntity
    {
        private Role() { }

        /// <summary>
        /// Displayed to the user, so system role names stay in Portuguese
        /// (Administrador, Gestor, Financeiro, Vendedor, Oficina). See ADR-0003.
        /// </summary>
        public string Name { get; private set; } = string.Empty;

        public string? Description { get; private set; }

        /// <summary>System roles cannot be deleted, but their screens can be adjusted.</summary>
        public bool IsSystem { get; private set; }

        public static Role Create(
            int idTenant,
            string name,
            string? description,
            bool isSystem = false,
            string createdBy = SystemActor)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Role name cannot be null or empty.", nameof(name));
            }

            var role = new Role(idTenant)
            {
                Name = name.Trim(),
                Description = description,
                IsSystem = isSystem
            };

            role.SetCreatedBy(createdBy);

            return role;
        }

        private Role(int idTenant) : base(idTenant) { }

        public void Update(string name, string? description, string updatedBy = SystemActor)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Role name cannot be null or empty.", nameof(name));
            }

            Name = name.Trim();
            Description = description;
            UpdateAuditInfo(updatedBy);
        }

        public bool CanBeDeleted => !IsSystem;
    }

    /// <summary>
    /// The permission itself: the row existing means "this role sees this screen".
    /// When action-level permission is needed, PodeEditar/PodeExcluir columns come here.
    /// </summary>
    [DebuggerDisplay("IdRole={IdRole}, IdScreen={IdScreen}")]
    public class RoleScreen : Entity
    {
        private RoleScreen() { }

        public int IdRole { get; private set; }

        public int IdScreen { get; private set; }

        public static RoleScreen Create(int idRole, int idScreen, string createdBy = SystemActor)
        {
            var link = new RoleScreen { IdRole = idRole, IdScreen = idScreen };
            link.SetCreatedBy(createdBy);

            return link;
        }
    }

    /// <summary>
    /// Link between user and role. Modelled as many-to-many; the interface assigns a
    /// single role per user in this phase. See ADR-0002.
    /// </summary>
    [DebuggerDisplay("IdUser={IdUser}, IdRole={IdRole}")]
    public class UserRole : Entity
    {
        private UserRole() { }

        public int IdUser { get; private set; }

        public int IdRole { get; private set; }

        public static UserRole Create(int idUser, int idRole, string createdBy = SystemActor)
        {
            var link = new UserRole { IdUser = idUser, IdRole = idRole };
            link.SetCreatedBy(createdBy);

            return link;
        }
    }
}
