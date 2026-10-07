using BusinessSearcher.Domain.BoundedContext.Radar.Enums;
using BusinessSearcher.Domain.Common;
using BusinessSearcher.Domain.Exceptions;

namespace BusinessSearcher.Domain.BoundedContext.Radar.Aggregates
{
    /// <summary>
    /// Relación de invitación entre un Client (que refiere) y un Tenant/MiPyme (referido).
    /// Vive en el bounded context Radar (igual que Referral, cliente-refiere-cliente) y
    /// solo guarda el Guid del Tenant, sin navegación cruzada — el mismo patrón de
    /// desacoplamiento entre bounded contexts que ya usa el resto del código. Se crea en
    /// estado Pending cuando la MiPyme se registra usando el código del cliente que la
    /// convenció, y pasa a Valid cuando un admin aprueba esa cuenta (mismo disparador que
    /// valida un Referral de cliente). Reusa el enum ReferralStatus existente.
    /// </summary>
    public class MipymeReferral : Entity, IAggregateRoot
    {
        public Guid           ReferrerClientId { get; private set; }
        public Guid           ReferredTenantId { get; private set; }
        public ReferralStatus Status           { get; private set; } = ReferralStatus.Pending;
        public DateTime?      ValidatedAt      { get; private set; }

        private MipymeReferral() { }

        public static MipymeReferral Create(Guid referrerClientId, Guid referredTenantId)
        {
            return new MipymeReferral
            {
                ReferrerClientId = referrerClientId,
                ReferredTenantId = referredTenantId,
                Status           = ReferralStatus.Pending
            };
        }

        /// <summary>Marca el referido como válido (el Tenant fue aprobado por un admin). Idempotente.</summary>
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
