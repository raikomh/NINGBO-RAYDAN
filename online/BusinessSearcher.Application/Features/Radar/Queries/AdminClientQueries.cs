using BusinessSearcher.Application.DTOs.Admin;
using BusinessSearcher.Application.DTOs.Common;
using BusinessSearcher.Domain.BoundedContext.Radar.Repositories;
using MediatR;

namespace BusinessSearcher.Application.Features.Radar.Queries
{
    // ═══════════════════════════════════════════════════════════════
    // Lista paginada de todos los clientes (con filtros) — panel de admin
    // ═══════════════════════════════════════════════════════════════
    public record GetAllClientsAdminQuery(
        int     Page       = 1,
        int     PageSize   = 20,
        string? Search     = null,
        bool?   IsApproved = null) : IRequest<PagedResult<AdminClientDto>>;

    public class GetAllClientsAdminQueryHandler
        : IRequestHandler<GetAllClientsAdminQuery, PagedResult<AdminClientDto>>
    {
        private readonly IClientRepository _clients;

        public GetAllClientsAdminQueryHandler(IClientRepository clients)
            => _clients = clients;

        public async Task<PagedResult<AdminClientDto>> Handle(
            GetAllClientsAdminQuery request, CancellationToken ct)
        {
            var pageSize = Math.Clamp(request.PageSize, 1, 100);
            var page     = Math.Max(request.Page, 1);

            var (items, total) = await _clients.GetAllWithFiltersAsync(
                page, pageSize, request.Search, request.IsApproved, ct);

            var dtos = items.Select(c => new AdminClientDto(
                c.Id, c.FullName, c.Email.Value, c.Plan.ToString(),
                c.IsEmailVerified, c.IsApproved, c.ReputationScore, c.ReportsSubmitted, c.CreatedAt,
                c.PremiumRequested, c.PhoneNumber, c.PremiumUntil));

            return new PagedResult<AdminClientDto>(dtos, total, page, pageSize);
        }
    }
}
