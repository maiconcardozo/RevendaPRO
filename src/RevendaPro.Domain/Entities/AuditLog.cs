using Foundation.Domain.Abstractions;
using System.Diagnostics;
using RevendaPro.Domain.Enums;

namespace RevendaPro.Domain.Entities
{
    [DebuggerDisplay("{EntityName} {Action} {RecordCode}")]
    public class AuditLog : TenantEntity
    {
        private AuditLog() { }

        private AuditLog(int idTenant) : base(idTenant) { }

        public int IdUser { get; private set; }

        public string EntityName { get; private set; } = string.Empty;

        public Guid RecordCode { get; private set; }

        public AuditAction Action { get; private set; }

        public string? OldValues { get; private set; }

        public string? NewValues { get; private set; }

        public static AuditLog Create(
            int idTenant,
            int idUser,
            string entityName,
            Guid recordCode,
            AuditAction action,
            string? oldValues,
            string? newValues)
        {
            var log = new AuditLog(idTenant)
            {
                IdUser = idUser,
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
