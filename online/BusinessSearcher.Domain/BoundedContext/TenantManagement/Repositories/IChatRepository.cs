using BusinessSearcher.Domain.BoundedContext.TenantManagement.Entities;

namespace BusinessSearcher.Domain.BoundedContext.TenantManagement.Repositories
{
    public interface IChatRepository
    {
        Task AddAsync(ChatMessage message, CancellationToken cancellationToken = default);

        Task<IEnumerable<ChatMessage>> GetConversationAsync(
            Guid tenantId, int page = 1, int pageSize = 50,
            CancellationToken cancellationToken = default);

        Task<int> CountConversationAsync(
            Guid tenantId, CancellationToken cancellationToken = default);

        Task<IEnumerable<ConversationSummary>> GetAllConversationsAsync(
            CancellationToken cancellationToken = default);

        Task MarkTenantMessagesReadAsync(
            Guid tenantId, CancellationToken cancellationToken = default);

        Task MarkAdminMessagesReadAsync(
            Guid tenantId, CancellationToken cancellationToken = default);

        Task<int> GetUnreadTenantCountAsync(
            Guid tenantId, CancellationToken cancellationToken = default);
    }

    public sealed class ConversationSummary
    {
        public Guid     TenantId          { get; init; }
        public string   TenantName        { get; init; } = default!;
        public string   LastMessage       { get; init; } = default!;
        public DateTime LastMessageAt     { get; init; }
        public bool     LastIsFromAdmin   { get; init; }
        public int      UnreadFromTenant  { get; init; }
    }
}
