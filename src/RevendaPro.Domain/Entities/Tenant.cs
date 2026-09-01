using System.Diagnostics;

namespace RevendaPro.Domain.Entities
{
    /// <summary>A dealership using the system. Every business row belongs to one.</summary>
    [DebuggerDisplay("Name={Name}, Id={Id}")]
    public class Tenant : BaseEntity
    {
        private Tenant() { }

        public string Name { get; private set; } = string.Empty;

        public static Tenant Create(string name, string createdBy = SystemActor)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Tenant name cannot be null or empty.", nameof(name));
            }

            var tenant = new Tenant { Name = name.Trim() };
            tenant.SetCreatedBy(createdBy);

            return tenant;
        }

        public void Rename(string name, string updatedBy = SystemActor)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Tenant name cannot be null or empty.", nameof(name));
            }

            Name = name.Trim();
            UpdateAuditInfo(updatedBy);
        }
    }
}
