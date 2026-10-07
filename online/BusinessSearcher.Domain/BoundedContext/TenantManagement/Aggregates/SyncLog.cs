using BusinessSearcher.Domain.BoundedContext.TenantManagement.Enums;
using BusinessSearcher.Domain.Common;
using BusinessSearcher.Domain.Exceptions;

namespace BusinessSearcher.Domain.BoundedContext.TenantManagement.Aggregates
{
    /// <summary>
    /// Auditoría de cada intento de sincronización entre una instalación local y el
    /// backend online (Render). Un tenant puede tener varios SyncLog a lo largo del
    /// tiempo; sirve para mostrar "última vez sincronizado" y diagnosticar fallos.
    /// </summary>
    public class SyncLog : Entity, IAggregateRoot
    {
        public Guid TenantId { get; private set; }
        public SyncDirection Direction { get; private set; }
        public SyncStatus Status { get; private set; }
        public DateTime StartedAt { get; private set; }
        public DateTime? CompletedAt { get; private set; }
        public int ItemsSent { get; private set; }
        public int ItemsAccepted { get; private set; }
        public string? ErrorMessage { get; private set; }
        public string? CounterpartyUrl { get; private set; }

        private SyncLog() { }

        public static SyncLog StartPush(Guid tenantId, string? counterpartyUrl)
        {
            if (tenantId == Guid.Empty)
                throw new DomainException("El TenantId del SyncLog es requerido.");

            return new SyncLog
            {
                TenantId        = tenantId,
                Direction       = SyncDirection.Push,
                Status          = SyncStatus.InProgress,
                StartedAt       = DateTime.UtcNow,
                CounterpartyUrl = counterpartyUrl
            };
        }

        /// <summary>Registra, del lado online, la recepción de un push subido por una instalación local.</summary>
        public static SyncLog StartPull(Guid tenantId, string? counterpartyUrl = null)
        {
            if (tenantId == Guid.Empty)
                throw new DomainException("El TenantId del SyncLog es requerido.");

            return new SyncLog
            {
                TenantId        = tenantId,
                Direction       = SyncDirection.Pull,
                Status          = SyncStatus.InProgress,
                StartedAt       = DateTime.UtcNow,
                CounterpartyUrl = counterpartyUrl
            };
        }

        public void Complete(int itemsSent, int itemsAccepted)
        {
            Status        = itemsAccepted >= itemsSent ? SyncStatus.Success : SyncStatus.PartialFailure;
            ItemsSent     = itemsSent;
            ItemsAccepted = itemsAccepted;
            CompletedAt   = DateTime.UtcNow;
            SetUpdated();
        }

        public void Fail(string errorMessage)
        {
            Status       = SyncStatus.Failed;
            ErrorMessage = errorMessage;
            CompletedAt  = DateTime.UtcNow;
            SetUpdated();
        }
    }
}
