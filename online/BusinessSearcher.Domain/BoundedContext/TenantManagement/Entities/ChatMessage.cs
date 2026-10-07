using BusinessSearcher.Domain.Common;
using BusinessSearcher.Domain.Exceptions;

namespace BusinessSearcher.Domain.BoundedContext.TenantManagement.Entities
{
    public enum ChatSenderType { Tenant, Admin }

    public class ChatMessage : Entity
    {
        public Guid           TenantId   { get; private set; }
        public string         SenderName { get; private set; } = default!;
        public ChatSenderType SenderType { get; private set; }
        public string         Text       { get; private set; } = default!;
        public bool           IsRead     { get; private set; }

        private ChatMessage() { }

        public static ChatMessage FromTenant(Guid tenantId, string businessName, string text)
        {
            if (tenantId == Guid.Empty)
                throw new DomainException("TenantId es requerido.");
            if (string.IsNullOrWhiteSpace(text))
                throw new DomainException("El mensaje no puede estar vacío.");
            if (text.Length > 2000)
                throw new DomainException("El mensaje no puede exceder 2000 caracteres.");

            return new ChatMessage
            {
                TenantId   = tenantId,
                SenderName = businessName.Trim(),
                SenderType = ChatSenderType.Tenant,
                Text       = text.Trim(),
                IsRead     = false
            };
        }

        public static ChatMessage FromAdmin(Guid tenantId, string adminName, string text)
        {
            if (tenantId == Guid.Empty)
                throw new DomainException("TenantId es requerido.");
            if (string.IsNullOrWhiteSpace(text))
                throw new DomainException("El mensaje no puede estar vacío.");
            if (text.Length > 2000)
                throw new DomainException("El mensaje no puede exceder 2000 caracteres.");

            return new ChatMessage
            {
                TenantId   = tenantId,
                SenderName = adminName.Trim(),
                SenderType = ChatSenderType.Admin,
                Text       = text.Trim(),
                IsRead     = false
            };
        }

        public void MarkRead()
        {
            IsRead = true;
            SetUpdated();
        }
    }
}
