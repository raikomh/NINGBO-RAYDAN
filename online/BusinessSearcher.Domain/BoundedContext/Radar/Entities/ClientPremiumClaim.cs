using BusinessSearcher.Domain.BoundedContext.Radar.Enums;
using BusinessSearcher.Domain.Common;
using BusinessSearcher.Domain.Exceptions;

namespace BusinessSearcher.Domain.BoundedContext.Radar.Entities
{
    /// <summary>
    /// Reporte de pago manual que el propio cliente envía para pasar a Premium (teléfono +
    /// monto + comprobante), pendiente de revisión por un admin. CreatedAt (heredado de
    /// Entity) es la fecha del reporte. Aprobarlo extiende el Premium vía
    /// Client.ApprovePremiumClaim, que reusa Client.ExtendPremium — sin mecanismo paralelo.
    /// Espejo de PaymentClaim (TenantManagement) para el mismo flujo del lado de Tenant.
    /// </summary>
    public class ClientPremiumClaim : Entity
    {
        public Guid ClientId { get; private set; }
        public string PhoneNumber { get; private set; } = default!;
        public decimal Amount { get; private set; }
        public string Currency { get; private set; } = default!;
        public string ProofReference { get; private set; } = default!;
        public PremiumClaimStatus Status { get; private set; }
        public DateTime? ReviewedAt { get; private set; }
        public string? ReviewNote { get; private set; }

        private ClientPremiumClaim() { }

        // internal: solo el Aggregate (Client.SubmitPremiumClaim) puede crear reportes
        internal ClientPremiumClaim(Guid clientId, string phoneNumber, decimal amount, string currency, string proofReference)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
                throw new DomainException("El número de teléfono es requerido.");
            if (amount <= 0)
                throw new DomainException("El monto debe ser mayor que cero.");
            if (string.IsNullOrWhiteSpace(proofReference))
                throw new DomainException("El comprobante (referencia del pago) es requerido.");

            ClientId       = clientId;
            PhoneNumber    = phoneNumber.Trim();
            Amount         = amount;
            Currency       = currency.ToUpperInvariant();
            ProofReference = proofReference.Trim();
            Status         = PremiumClaimStatus.Pending;
        }

        internal void Approve(string? note)
        {
            if (Status != PremiumClaimStatus.Pending)
                throw new DomainException("Este reporte de pago ya fue revisado.");
            Status     = PremiumClaimStatus.Approved;
            ReviewedAt = DateTime.UtcNow;
            ReviewNote = note;
        }

        internal void Reject(string? note)
        {
            if (Status != PremiumClaimStatus.Pending)
                throw new DomainException("Este reporte de pago ya fue revisado.");
            Status     = PremiumClaimStatus.Rejected;
            ReviewedAt = DateTime.UtcNow;
            ReviewNote = note;
        }
    }
}
