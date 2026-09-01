using Foundation.Domain.Abstractions;

namespace RevendaPro.Domain.Entities
{
    /// <summary>
    /// Base for every persisted entity.
    ///
    /// Exists for one reason: Foundation's <see cref="Entity"/> seeds <c>Code</c> with a
    /// random v4 GUID. As a public identifier that is fine, but the column is indexed and
    /// v4 is not time-ordered. This constructor replaces it with UUID v7, which is.
    /// </summary>
    public abstract class BaseEntity : Entity
    {
        protected BaseEntity()
        {
            Code = Guid.CreateVersion7();
            SetCreatedBy(SystemActor);
        }

        /// <summary>Actor recorded when a row is written by the system, not by a person.</summary>
        public const string SystemActor = "System";

        /// <summary>Reads soft deletion by what it means, instead of negating IsActive everywhere.</summary>
        public bool IsDeleted => !IsActive;

        public void RegisterUpdate(string updatedBy = SystemActor) => UpdateAuditInfo(updatedBy);

        public void Delete(string deletedBy = SystemActor)
        {
            if (IsDeleted)
            {
                return;
            }

            SoftDelete(deletedBy);
        }

        public void Restore(string updatedBy = SystemActor)
        {
            Activate();
            UpdateAuditInfo(updatedBy);
        }
    }

    /// <summary>Entity owned by a tenant. Every business row must be isolated by TenantId.</summary>
    public abstract class TenantEntity : BaseEntity
    {
        protected TenantEntity() { }

        protected TenantEntity(int tenantId) => TenantId = tenantId;

        public int TenantId { get; protected set; }
    }
}
