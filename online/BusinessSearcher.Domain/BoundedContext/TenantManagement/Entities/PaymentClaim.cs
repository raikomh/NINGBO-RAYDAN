using BusinessSearcher.Domain.BoundedContext.TenantManagement.Enums;
using BusinessSearcher.Domain.Common;
using BusinessSearcher.Domain.Exceptions;

namespace BusinessSearcher.Domain.BoundedContext.TenantManagement.Entities
{
    /// <summary>
    /// Reporte de pago manual que el propio tenant envía (teléfono + monto + comprobante),
    /// pendiente de revisión por un admin. CreatedAt (heredado de Entity) es la fecha del
    /// reporte. Aprobarlo registra el pago real vía Tenant.ApprovePaymentClaim, que reusa
    /// Tenant.RegisterPayment — sin mecanismo paralelo de extensión de suscripción.
    /// </summary>
    public class PaymentClaim : Entity
    {
        public Guid TenantId { get; private set; }
        public string PhoneNumber { get; private set; } = default!;
        public decimal Amount { get; private set; }
        public string Currency { get; private set; } = default!;
        public string ProofReference { get; private set; } = default!;
        public PaymentClaimStatus Status { get; private set; }
        public DateTime? ReviewedAt { get; private set; }
        public string? ReviewNote { get; private set; }

        private PaymentClaim() { }

        // internal: solo el Aggregate (Tenant.SubmitPaymentClaim) puede crear reportes
        internal PaymentClaim(Guid tenantId, string phoneNumber, decimal amount, string currency, string proofReference)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
                throw new DomainException("El número de teléfono es requerido.");
            if (amount <= 0)
                throw new DomainException("El monto debe ser mayor que cero.");
            if (string.IsNullOrWhiteSpace(proofReference))
                throw new DomainException("El comprobante (referencia del pago) es requerido.");

            TenantId       = tenantId;
            PhoneNumber    = phoneNumber.Trim();
            Amount         = amount;
            Currency       = currency.ToUpperInvariant();
            ProofReference = proofReference.Trim();
            Status         = PaymentClaimStatus.Pending;
        }

        internal void Approve(string? note)
        {
            if (Status != PaymentClaimStatus.Pending)
                throw new DomainException("Este reporte de pago ya fue revisado.");
            Status     = PaymentClaimStatus.Approved;
            ReviewedAt = DateTime.UtcNow;
            ReviewNote = note;
        }

        internal void Reject(string? note)
        {
            if (Status != PaymentClaimStatus.Pending)
                throw new DomainException("Este reporte de pago ya fue revisado.");
            Status     = PaymentClaimStatus.Rejected;
            ReviewedAt = DateTime.UtcNow;
            ReviewNote = note;
        }
    }
}
