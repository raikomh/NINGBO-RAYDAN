using BusinessSearcher.Application.Commons.Interfaces;
using BusinessSearcher.Application.Features.Radar.Commands.SetClientPlan;
using BusinessSearcher.Domain.BoundedContext.Radar.Repositories;
using BusinessSearcher.Domain.Exceptions;
using MediatR;

namespace BusinessSearcher.Application.Features.Radar.Commands
{
    // ═══════════════════════════════════════════════════════════════
    // APPROVE CLIENT — habilita el login de un cliente recién registrado
    // ═══════════════════════════════════════════════════════════════
    public record AdminApproveClientCommand(Guid ClientId) : IRequest;

    public class AdminApproveClientCommandHandler : IRequestHandler<AdminApproveClientCommand>
    {
        private readonly IClientRepository   _clients;
        private readonly IReferralRepository _referrals;
        private readonly IRadarUnitOfWork    _uow;

        public AdminApproveClientCommandHandler(
            IClientRepository clients, IReferralRepository referrals, IRadarUnitOfWork uow)
        {
            _clients   = clients;
            _referrals = referrals;
            _uow       = uow;
        }

        public async Task Handle(AdminApproveClientCommand request, CancellationToken ct)
        {
            var client = await _clients.GetByIdAsync(request.ClientId, ct)
                ?? throw new DomainException("Cliente no encontrado.");

            client.Approve();
            await _clients.UpdateAsync(client, ct);

            // La aprobación es el disparador principal de validación de referidos: si
            // este cliente se registró con un código de invitación, esto valida el
            // referido y premia (días Premium + CUP) a quien lo invitó.
            await ReferralReward.ValidateAndRewardAsync(client.Id, _clients, _referrals, ct);

            await _uow.SaveChangesAsync(ct);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // REJECT CLIENT — revoca la aprobación y bloquea el login
    // ═══════════════════════════════════════════════════════════════
    public record AdminRejectClientCommand(Guid ClientId) : IRequest;

    public class AdminRejectClientCommandHandler : IRequestHandler<AdminRejectClientCommand>
    {
        private readonly IClientRepository _clients;
        private readonly IRadarUnitOfWork  _uow;

        public AdminRejectClientCommandHandler(IClientRepository clients, IRadarUnitOfWork uow)
        {
            _clients = clients;
            _uow     = uow;
        }

        public async Task Handle(AdminRejectClientCommand request, CancellationToken ct)
        {
            var client = await _clients.GetByIdAsync(request.ClientId, ct)
                ?? throw new DomainException("Cliente no encontrado.");

            client.Reject();
            await _clients.UpdateAsync(client, ct);
            await _uow.SaveChangesAsync(ct);
        }
    }
}
