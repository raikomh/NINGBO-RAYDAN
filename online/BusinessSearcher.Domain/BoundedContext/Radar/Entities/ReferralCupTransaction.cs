using BusinessSearcher.Domain.BoundedContext.Radar.Enums;
using BusinessSearcher.Domain.Common;
using BusinessSearcher.Domain.Exceptions;

namespace BusinessSearcher.Domain.BoundedContext.Radar.Entities
{
    /// <summary>
    /// Movimiento de auditoría del saldo de CUP por referidos de un cliente
    /// (colgado de <see cref="Aggregates.Client"/>, mismo molde que TenantPayment).
    /// </summary>
    public class ReferralCupTransaction : Entity
    {
        public Guid ClientId { get; private set; }
        public decimal Amount { get; private set; }
        public ReferralCupTransactionType Type { get; private set; }
        public string? Note { get; private set; }

        private ReferralCupTransaction() { }

        // internal: solo Client puede crear movimientos de su propio saldo.
        internal ReferralCupTransaction(Guid clientId, decimal amount, ReferralCupTransactionType type, string? note = null)
        {
            if (amount == 0)
                throw new DomainException("El monto del movimiento no puede ser cero.");

            ClientId = clientId;
            Amount   = amount;
            Type     = type;
            Note     = note;
        }
    }
}
