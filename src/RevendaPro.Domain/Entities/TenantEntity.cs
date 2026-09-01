using Foundation.Domain.Abstractions;

namespace RevendaPro.Domain.Entities
{
    /// <summary>
    /// Entity owned by a tenant. Every business row must be isolated by <see cref="TenantId"/>.
    ///
    /// This is the only base this project adds on top of Foundation's <see cref="Entity"/>:
    /// multi-tenancy is a decision of this application, not of the library. Everything else -
    /// the identifier pair, the audit trail, the soft delete - comes from Foundation.
    /// </summary>
    public abstract class TenantEntity : Entity
    {
        protected TenantEntity() { }

        protected TenantEntity(int tenantId) => TenantId = tenantId;

        public int TenantId { get; protected set; }
    }
}
