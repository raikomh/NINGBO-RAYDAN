using BusinessSearcher.Domain.BoundedContext.TenantManagement.Entities;
using BusinessSearcher.Domain.BoundedContext.TenantManagement.Repositories;
using BusinessSearcher.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace BusinessSearcher.Infrastructure.Persistence.Repositories.TenantManagement
{
    public class ChatRepository : IChatRepository
    {
        private readonly TenantManagementDbContext _context;

        public ChatRepository(TenantManagementDbContext context)
            => _context = context;

        public async Task AddAsync(ChatMessage message, CancellationToken cancellationToken = default)
            => await _context.ChatMessages.AddAsync(message, cancellationToken);

        public async Task<IEnumerable<ChatMessage>> GetConversationAsync(
            Guid tenantId, int page = 1, int pageSize = 50,
            CancellationToken cancellationToken = default)
            => await _context.ChatMessages
                .Where(m => m.TenantId == tenantId)
                .OrderBy(m => m.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

        public async Task<int> CountConversationAsync(
            Guid tenantId, CancellationToken cancellationToken = default)
            => await _context.ChatMessages
                .CountAsync(m => m.TenantId == tenantId, cancellationToken);

        public async Task<IEnumerable<ConversationSummary>> GetAllConversationsAsync(
            CancellationToken cancellationToken = default)
        {
            var lastMessages = await _context.ChatMessages
                .GroupBy(m => m.TenantId)
                .Select(g => new
                {
                    TenantId         = g.Key,
                    LastMessage      = g.OrderByDescending(m => m.CreatedAt).First().Text,
                    LastMessageAt    = g.OrderByDescending(m => m.CreatedAt).First().CreatedAt,
                    LastSenderName   = g.OrderByDescending(m => m.CreatedAt).First().SenderName,
                    LastIsFromAdmin  = g.OrderByDescending(m => m.CreatedAt).First().SenderType == ChatSenderType.Admin,
                    UnreadFromTenant = g.Count(m => m.SenderType == ChatSenderType.Tenant && !m.IsRead)
                })
                .OrderByDescending(x => x.LastMessageAt)
                .ToListAsync(cancellationToken);

            return lastMessages.Select(x => new ConversationSummary
            {
                TenantId         = x.TenantId,
                TenantName       = x.LastSenderName,
                LastMessage      = x.LastMessage,
                LastMessageAt    = x.LastMessageAt,
                LastIsFromAdmin  = x.LastIsFromAdmin,
                UnreadFromTenant = x.UnreadFromTenant,
            });
        }

        public async Task MarkTenantMessagesReadAsync(
            Guid tenantId, CancellationToken cancellationToken = default)
        {
            var unread = await _context.ChatMessages
                .Where(m => m.TenantId == tenantId
                    && m.SenderType == ChatSenderType.Tenant
                    && !m.IsRead)
                .ToListAsync(cancellationToken);

            foreach (var msg in unread)
                msg.MarkRead();
        }

        public async Task MarkAdminMessagesReadAsync(
            Guid tenantId, CancellationToken cancellationToken = default)
        {
            var unread = await _context.ChatMessages
                .Where(m => m.TenantId == tenantId
                    && m.SenderType == ChatSenderType.Admin
                    && !m.IsRead)
                .ToListAsync(cancellationToken);

            foreach (var msg in unread)
                msg.MarkRead();
        }

        public async Task<int> GetUnreadTenantCountAsync(
            Guid tenantId, CancellationToken cancellationToken = default)
            => await _context.ChatMessages
                .CountAsync(m => m.TenantId == tenantId
                    && m.SenderType == ChatSenderType.Admin
                    && !m.IsRead,
                    cancellationToken);
    }
}
