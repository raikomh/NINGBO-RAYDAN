using BusinessSearcher.Domain.BoundedContext.Radar.Enums;
using BusinessSearcher.Domain.Common;
using BusinessSearcher.Domain.Exceptions;

namespace BusinessSearcher.Domain.BoundedContext.Radar.Aggregates
{
    /// <summary>
    /// Relación de invitación entre dos clientes. Se crea en estado Pending cuando
    /// el invitado se registra usando el código del que invita, y pasa a Valid
    /// cuando el invitado se hace premium (paga). Solo entonces cuenta para la
    /// recompensa del que invitó.
    /// </summary>
    public class Referral : Entity, IAggregateRoot
    {
        public Guid           InviterClientId { get; private set; }
        public Guid           InvitedClientId { get; private set; }
        public ReferralStatus Status          { get; private set; } = ReferralStatus.Pending;
        public DateTime?      ValidatedAt     { get; private set; }

        private Referral() { }

        public static Referral Create(Guid inviterClientId, Guid invitedClientId)
        {
            if (inviterClientId == invitedClientId)
                throw new DomainException("No puedes referirte a ti mismo.");

            return new Referral
            {
                InviterClientId = inviterClientId,
                InvitedClientId = invitedClientId,
                Status          = ReferralStatus.Pending
            };
        }

        /// <summary>Marca el referido como válido (el invitado pasó a premium). Idempotente.</summary>
        public bool MarkValid()
        {
            if (Status != ReferralStatus.Pending) return false;
            Status      = ReferralStatus.Valid;
            ValidatedAt = DateTime.UtcNow;
            SetUpdated();
            return true;
        }

        public void Reject()
        {
            if (Status == ReferralStatus.Valid) return;
            Status = ReferralStatus.Rejected;
            SetUpdated();
        }
    }
}
