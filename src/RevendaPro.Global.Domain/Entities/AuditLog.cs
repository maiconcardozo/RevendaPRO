using System.Diagnostics;
using RevendaPro.Global.Domain.Enums;

namespace RevendaPro.Global.Domain.Entities
{
    [DebuggerDisplay("{EntityName} {Action} {RecordCode}")]
    public class AuditLog : TenantEntity
    {
        private AuditLog() { }

        private AuditLog(int tenantId) : base(tenantId) { }

        public int UserId { get; private set; }

        public string EntityName { get; private set; } = string.Empty;

        public Guid RecordCode { get; private set; }

        public AuditAction Action { get; private set; }

        public string? OldValues { get; private set; }

        public string? NewValues { get; private set; }

        public static AuditLog Create(
            int tenantId,
            int userId,
            string entityName,
            Guid recordCode,
            AuditAction action,
            string? oldValues,
            string? newValues)
        {
            var log = new AuditLog(tenantId)
            {
                UserId = userId,
                EntityName = entityName,
                RecordCode = recordCode,
                Action = action,
                OldValues = oldValues,
                NewValues = newValues
            };

            log.SetCreatedBy(SystemActor);

            return log;
        }
    }
}
