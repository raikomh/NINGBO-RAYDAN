using BusinessSearcher.Application.Commons.Interfaces;
using BusinessSearcher.Application.DTOs.Chat;
using BusinessSearcher.Application.DTOs.Common;
using BusinessSearcher.Domain.BoundedContext.TenantManagement.Repositories;
using MediatR;

namespace BusinessSearcher.Application.Features.TenantManagement.Queries
{
    // ═══════════════════════════════════════════════════════════════
    // TENANT: get own conversation with admin
    // ═══════════════════════════════════════════════════════════════
    public record GetMyConversationQuery(int Page = 1, int PageSize = 50)
        : IRequest<PagedResult<ChatMessageDto>>;

    public class GetMyConversationQueryHandler
        : IRequestHandler<GetMyConversationQuery, PagedResult<ChatMessageDto>>
    {
        private readonly IChatRepository     _chatRepo;
        private readonly ICurrentUserService _currentUser;

        public GetMyConversationQueryHandler(IChatRepository chatRepo, ICurrentUserService currentUser)
        {
            _chatRepo    = chatRepo;
            _currentUser = currentUser;
        }

        public async Task<PagedResult<ChatMessageDto>> Handle(
            GetMyConversationQuery request, CancellationToken cancellationToken)
        {
            var pageSize = Math.Clamp(request.PageSize, 1, 100);
            var page     = Math.Max(request.Page, 1);

            var messages = await _chatRepo.GetConversationAsync(
                _currentUser.TenantId, page, pageSize, cancellationToken);

            var total = await _chatRepo.CountConversationAsync(
                _currentUser.TenantId, cancellationToken);

            var dtos = messages.Select(m => new ChatMessageDto(
                m.Id, m.SenderName, m.SenderType.ToString(),
                m.Text, m.IsRead, m.CreatedAt));

            return new PagedResult<ChatMessageDto>(dtos, total, page, pageSize);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // TENANT: get unread count (admin messages not yet seen)
    // ═══════════════════════════════════════════════════════════════
    public record GetUnreadCountQuery : IRequest<UnreadCountDto>;

    public class GetUnreadCountQueryHandler : IRequestHandler<GetUnreadCountQuery, UnreadCountDto>
    {
        private readonly IChatRepository     _chatRepo;
        private readonly ICurrentUserService _currentUser;

        public GetUnreadCountQueryHandler(IChatRepository chatRepo, ICurrentUserService currentUser)
        {
            _chatRepo    = chatRepo;
            _currentUser = currentUser;
        }

        public async Task<UnreadCountDto> Handle(
            GetUnreadCountQuery request, CancellationToken cancellationToken)
        {
            var count = await _chatRepo.GetUnreadTenantCountAsync(
                _currentUser.TenantId, cancellationToken);
            return new UnreadCountDto(count);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // ADMIN: list all conversations (one per tenant, sorted by last msg)
    // ═══════════════════════════════════════════════════════════════
    public record GetAllConversationsQuery : IRequest<IEnumerable<ConversationSummaryDto>>;

    public class GetAllConversationsQueryHandler
        : IRequestHandler<GetAllConversationsQuery, IEnumerable<ConversationSummaryDto>>
    {
        private readonly IChatRepository _chatRepo;

        public GetAllConversationsQueryHandler(IChatRepository chatRepo)
            => _chatRepo = chatRepo;

        public async Task<IEnumerable<ConversationSummaryDto>> Handle(
            GetAllConversationsQuery request, CancellationToken cancellationToken)
        {
            var summaries = await _chatRepo.GetAllConversationsAsync(cancellationToken);
            return summaries.Select(s => new ConversationSummaryDto(
                s.TenantId, s.TenantName, s.LastMessage,
                s.LastMessageAt, s.LastIsFromAdmin, s.UnreadFromTenant));
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // ADMIN: get conversation with a specific tenant
    // ═══════════════════════════════════════════════════════════════
    public record GetTenantConversationQuery(Guid TenantId, int Page = 1, int PageSize = 50)
        : IRequest<PagedResult<ChatMessageDto>>;

    public class GetTenantConversationQueryHandler
        : IRequestHandler<GetTenantConversationQuery, PagedResult<ChatMessageDto>>
    {
        private readonly IChatRepository _chatRepo;

        public GetTenantConversationQueryHandler(IChatRepository chatRepo)
            => _chatRepo = chatRepo;

        public async Task<PagedResult<ChatMessageDto>> Handle(
            GetTenantConversationQuery request, CancellationToken cancellationToken)
        {
            var pageSize = Math.Clamp(request.PageSize, 1, 100);
            var page     = Math.Max(request.Page, 1);

            var messages = await _chatRepo.GetConversationAsync(
                request.TenantId, page, pageSize, cancellationToken);

            var total = await _chatRepo.CountConversationAsync(
                request.TenantId, cancellationToken);

            var dtos = messages.Select(m => new ChatMessageDto(
                m.Id, m.SenderName, m.SenderType.ToString(),
                m.Text, m.IsRead, m.CreatedAt));

            return new PagedResult<ChatMessageDto>(dtos, total, page, pageSize);
        }
    }
}
