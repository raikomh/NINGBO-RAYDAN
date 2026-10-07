using BusinessSearcher.Application.Commons.Interfaces;
using BusinessSearcher.Application.DTOs.Radar;
using BusinessSearcher.Application.Features.Radar.Commands.ClientAuth;
using BusinessSearcher.Domain.BoundedContext.Radar.Enums;
using BusinessSearcher.Domain.BoundedContext.Radar.Repositories;
using BusinessSearcher.Domain.Exceptions;
using FluentValidation;
using MediatR;

namespace BusinessSearcher.Application.Features.Radar.Commands.SetClientPlan
{
    /// <summary>
    /// Cambia el plan de un cliente (Normal ↔ Premium). Acción de administración:
    /// mientras no exista el pago de clientes, el Premium se concede manualmente
    /// (p. ej. como recompensa a corresponsales locales de buena reputación).
    /// </summary>
    public record SetClientPlanCommand(Guid ClientId, bool Premium) : IRequest<ClientDto>;

    public class SetClientPlanCommandHandler : IRequestHandler<SetClientPlanCommand, ClientDto>
    {
        private readonly IClientRepository   _clients;
        private readonly IReferralRepository _referrals;
        private readonly IRadarUnitOfWork    _uow;

        public SetClientPlanCommandHandler(
            IClientRepository clients, IReferralRepository referrals, IRadarUnitOfWork uow)
        {
            _clients = clients; _referrals = referrals; _uow = uow;
        }

        public async Task<ClientDto> Handle(SetClientPlanCommand request, CancellationToken ct)
        {
            var client = await _clients.GetByIdAsync(request.ClientId, ct)
                ?? throw new DomainException("Cliente no encontrado.");

            var wasPremium = client.Plan == Domain.BoundedContext.Radar.Enums.ClientPlan.Premium;

            if (request.Premium)
                client.UpgradeToPremium();
            else
                client.DowngradeToNormal();

            await _clients.UpdateAsync(client, ct);

            // Al pasar a premium (paga), valida el referido pendiente (si el admin aún
            // no lo había validado por aprobación) y premia al que invitó.
            if (request.Premium && !wasPremium)
                await ReferralReward.ValidateAndRewardAsync(client.Id, _clients, _referrals, ct);

            await _uow.SaveChangesAsync(ct);

            return ClientMapper.ToDto(client);
        }
    }

    /// <summary>
    /// El cliente autenticado pide pasar a Premium desde la app. Queda pendiente de
    /// revisión de un admin (no otorga el plan directamente).
    /// </summary>
    public record RequestPremiumCommand(Guid ClientId) : IRequest<ClientDto>;

    public class RequestPremiumCommandHandler : IRequestHandler<RequestPremiumCommand, ClientDto>
    {
        private readonly IClientRepository _clients;
        private readonly IRadarUnitOfWork  _uow;

        public RequestPremiumCommandHandler(IClientRepository clients, IRadarUnitOfWork uow)
        { _clients = clients; _uow = uow; }

        public async Task<ClientDto> Handle(RequestPremiumCommand request, CancellationToken ct)
        {
            var client = await _clients.GetByIdAsync(request.ClientId, ct)
                ?? throw new DomainException("Cliente no encontrado.");

            client.RequestPremium();
            await _clients.UpdateAsync(client, ct);
            await _uow.SaveChangesAsync(ct);

            return ClientMapper.ToDto(client);
        }
    }

    /// <summary>Un admin rechaza la solicitud de Premium de un cliente (se queda en Normal).</summary>
    public record AdminRejectPremiumRequestCommand(Guid ClientId) : IRequest;

    public class AdminRejectPremiumRequestCommandHandler : IRequestHandler<AdminRejectPremiumRequestCommand>
    {
        private readonly IClientRepository _clients;
        private readonly IRadarUnitOfWork  _uow;

        public AdminRejectPremiumRequestCommandHandler(IClientRepository clients, IRadarUnitOfWork uow)
        { _clients = clients; _uow = uow; }

        public async Task Handle(AdminRejectPremiumRequestCommand request, CancellationToken ct)
        {
            var client = await _clients.GetByIdAsync(request.ClientId, ct)
                ?? throw new DomainException("Cliente no encontrado.");

            client.RejectPremiumRequest();
            await _clients.UpdateAsync(client, ct);
            await _uow.SaveChangesAsync(ct);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // SUBMIT PREMIUM CLAIM — el propio cliente reporta un pago (autoservicio:
    // teléfono + monto + comprobante). Espejo de SubmitPaymentClaimCommand (Tenant).
    // ═══════════════════════════════════════════════════════════════
    public record SubmitPremiumClaimCommand(
        Guid ClientId, string PhoneNumber, decimal Amount, string? Currency, string ProofReference) : IRequest<Guid>;

    public class SubmitPremiumClaimCommandValidator : AbstractValidator<SubmitPremiumClaimCommand>
    {
        public SubmitPremiumClaimCommandValidator()
        {
            RuleFor(x => x.PhoneNumber)
                .NotEmpty().WithMessage("El número de teléfono es requerido.")
                .MaximumLength(30);
            RuleFor(x => x.Amount)
                .GreaterThan(0).WithMessage("El monto debe ser mayor que cero.");
            RuleFor(x => x.ProofReference)
                .NotEmpty().WithMessage("Indica una referencia del comprobante (número de operación, remitente, etc.).")
                .MaximumLength(300);
        }
    }

    public class SubmitPremiumClaimCommandHandler : IRequestHandler<SubmitPremiumClaimCommand, Guid>
    {
        private readonly IClientRepository _clients;
        private readonly IRadarUnitOfWork  _uow;

        public SubmitPremiumClaimCommandHandler(IClientRepository clients, IRadarUnitOfWork uow)
        { _clients = clients; _uow = uow; }

        public async Task<Guid> Handle(SubmitPremiumClaimCommand request, CancellationToken ct)
        {
            var client = await _clients.GetByIdAsync(request.ClientId, ct)
                ?? throw new DomainException("Cliente no encontrado.");

            var claim = client.SubmitPremiumClaim(
                request.PhoneNumber, request.Amount, request.Currency ?? "CUP", request.ProofReference);

            await _clients.UpdateAsync(client, ct);
            await _uow.SaveChangesAsync(ct);
            return claim.Id;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // ADMIN APPROVE — extiende el Premium N días (default 30) y cierra el reporte
    // ═══════════════════════════════════════════════════════════════
    public record AdminApprovePremiumClaimCommand(Guid ClientId, Guid ClaimId, string? Note, int Days = 30) : IRequest;

    public class AdminApprovePremiumClaimCommandHandler : IRequestHandler<AdminApprovePremiumClaimCommand>
    {
        private readonly IClientRepository _clients;
        private readonly IRadarUnitOfWork  _uow;

        public AdminApprovePremiumClaimCommandHandler(IClientRepository clients, IRadarUnitOfWork uow)
        { _clients = clients; _uow = uow; }

        public async Task Handle(AdminApprovePremiumClaimCommand request, CancellationToken ct)
        {
            var client = await _clients.GetByIdAsync(request.ClientId, ct)
                ?? throw new DomainException("Cliente no encontrado.");

            client.ApprovePremiumClaim(request.ClaimId, request.Days <= 0 ? 30 : request.Days, request.Note);

            await _clients.UpdateAsync(client, ct);
            await _uow.SaveChangesAsync(ct);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // ADMIN REJECT — cierra el reporte sin conceder Premium
    // ═══════════════════════════════════════════════════════════════
    public record AdminRejectPremiumClaimCommand(Guid ClientId, Guid ClaimId, string? Note) : IRequest;

    public class AdminRejectPremiumClaimCommandHandler : IRequestHandler<AdminRejectPremiumClaimCommand>
    {
        private readonly IClientRepository _clients;
        private readonly IRadarUnitOfWork  _uow;

        public AdminRejectPremiumClaimCommandHandler(IClientRepository clients, IRadarUnitOfWork uow)
        { _clients = clients; _uow = uow; }

        public async Task Handle(AdminRejectPremiumClaimCommand request, CancellationToken ct)
        {
            var client = await _clients.GetByIdAsync(request.ClientId, ct)
                ?? throw new DomainException("Cliente no encontrado.");

            client.RejectPremiumClaim(request.ClaimId, request.Note);

            await _clients.UpdateAsync(client, ct);
            await _uow.SaveChangesAsync(ct);
        }
    }

    /// <summary>
    /// Lógica compartida de recompensa de referidos. Se dispara desde dos puntos posibles
    /// — la aprobación de cuenta del admin (disparador principal, ver
    /// AdminApproveClientCommandHandler) o el paso a Premium (ver SetClientPlanCommandHandler) —
    /// y es idempotente: Referral.MarkValid() no hace nada si el referido ya estaba validado,
    /// así que no hay riesgo de premiar dos veces si ambos eventos llegan a ocurrir.
    /// Al validarse un referido: sube el contador del que invitó, le extiende el premium si
    /// alcanza un nivel mensual (10/25/50/100/250) y le acredita 10 CUP al saldo (desde el primero).
    /// </summary>
    public static class ReferralReward
    {
        // Umbral mensual → días de premium que otorga al alcanzarlo.
        public static readonly (int threshold, int days)[] Tiers =
        {
            (10, 7), (25, 15), (50, 30), (100, 60), (250, 90)
        };

        /// <summary>CUP fijos que gana el invitador por cada referido validado, desde el primero.</summary>
        public const decimal CupPerValidReferral = 10m;

        public static async Task ValidateAndRewardAsync(
            Guid invitedClientId, IClientRepository clients, IReferralRepository referrals, CancellationToken ct)
        {
            var referral = await referrals.GetPendingByInvitedAsync(invitedClientId, ct);
            if (referral is null || !referral.MarkValid())
                return;

            await referrals.UpdateAsync(referral, ct);

            var inviter = await clients.GetByIdAsync(referral.InviterClientId, ct);
            if (inviter is null) return;

            var (_, thisMonth) = inviter.RecordValidReferral(DateTime.UtcNow);

            // Si el nuevo total mensual alcanza justo un umbral, otorga sus días de premium.
            var reward = Tiers.FirstOrDefault(t => t.threshold == thisMonth);
            if (reward.days > 0)
                inviter.ExtendPremium(reward.days);

            inviter.CreditReferralCup(CupPerValidReferral, ReferralCupTransactionType.PerReferralBonus);

            await clients.UpdateAsync(inviter, ct);
        }
    }
}
