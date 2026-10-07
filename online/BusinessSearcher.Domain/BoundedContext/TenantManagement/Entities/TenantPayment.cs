using BusinessSearcher.Domain.Common;
using BusinessSearcher.Domain.Exceptions;

namespace BusinessSearcher.Domain.BoundedContext.TenantManagement.Entities
{
    public class TenantPayment : Entity
    {
        public Guid TenantId { get; private set; }
        public decimal Amount { get; private set; }
        public string Currency { get; private set; } = default!;
        public string? Reference { get; private set; }
        public DateTime PaymentDate { get; private set; }

        private TenantPayment() { }

        // CORRECCIÓN: internal para que solo el Aggregate pueda crear pagos
        internal TenantPayment(Guid tenantId, decimal amount, string currency = "COP", string? reference = null)
        {
            // CORRECCIÓN: validación corregida (debe ser > 0, no >= 0)
            if (amount <= 0)
                throw new DomainException("El monto del pago debe ser mayor que cero.");

            TenantId    = tenantId;
            Amount      = amount;
            Currency    = currency.ToUpperInvariant();
            Reference   = reference;
            PaymentDate = DateTime.UtcNow;
        }
    }
}
