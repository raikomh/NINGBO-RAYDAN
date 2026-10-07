using BusinessSearcher.Application.Commons.Interfaces;
using BusinessSearcher.Domain.BoundedContext.TenantManagement.Aggregates;
using BusinessSearcher.Domain.BoundedContext.TenantManagement.Repositories;
using FluentValidation;
using MediatR;

namespace BusinessSearcher.Application.Features.TenantManagement.Commands.Marketing
{
    // ── Registrar una visita con origen de campaña (?src=grupo_habana, etc.) ──
    public record RecordMarketingVisitCommand(string Source) : IRequest;

    public class RecordMarketingVisitCommandValidator : AbstractValidator<RecordMarketingVisitCommand>
    {
        public RecordMarketingVisitCommandValidator()
        {
            RuleFor(x => x.Source).NotEmpty().MaximumLength(100);
        }
    }

    public class RecordMarketingVisitCommandHandler : IRequestHandler<RecordMarketingVisitCommand>
    {
        private readonly IMarketingVisitRepository _repo;
        private readonly IUnitOfWork _unitOfWork;

        public RecordMarketingVisitCommandHandler(IMarketingVisitRepository repo, IUnitOfWork unitOfWork)
        {
            _repo = repo;
            _unitOfWork = unitOfWork;
        }

        public async Task Handle(RecordMarketingVisitCommand request, CancellationToken cancellationToken)
        {
            var visit = MarketingVisit.Create(request.Source);
            await _repo.AddAsync(visit, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}

namespace BusinessSearcher.Application.Features.TenantManagement.Queries.Marketing
{
    public record MarketingVisitSummaryDto(string Source, int VisitCount, DateTime LastVisitAt);

    public record GetMarketingVisitsSummaryQuery : IRequest<IReadOnlyList<MarketingVisitSummaryDto>>;

    public class GetMarketingVisitsSummaryQueryHandler
        : IRequestHandler<GetMarketingVisitsSummaryQuery, IReadOnlyList<MarketingVisitSummaryDto>>
    {
        private readonly IMarketingVisitRepository _repo;
        public GetMarketingVisitsSummaryQueryHandler(IMarketingVisitRepository repo) => _repo = repo;

        public async Task<IReadOnlyList<MarketingVisitSummaryDto>> Handle(
            GetMarketingVisitsSummaryQuery request, CancellationToken cancellationToken)
        {
            var summary = await _repo.GetSummaryAsync(cancellationToken);
            return summary.Select(s => new MarketingVisitSummaryDto(s.Source, s.VisitCount, s.LastVisitAt)).ToList();
        }
    }
}
