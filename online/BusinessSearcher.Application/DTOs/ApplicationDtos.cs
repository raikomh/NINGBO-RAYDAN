namespace BusinessSearcher.Application.DTOs.TenantManagement
{
    public record TenantDto(
        Guid   Id,
        string BusinessName,
        string Email,
        string Status,
        bool   IsSubscriptionActive,
        DateTime CreatedAt,
        DateTime? NextPaymentDate,
        decimal TotalPaid,
        string Plan = "Standard",
        string TenantType = "Retail",
        string? PhoneNumber = null,
        DateTime? LastPaymentDate = null);

    public record PaymentClaimDto(
        Guid     Id,
        decimal  Amount,
        string   Currency,
        string   ProofReference,
        string   PhoneNumber,
        string   Status,
        DateTime RequestedAt,
        DateTime? ReviewedAt,
        string?  ReviewNote);

    public record AuthResultDto(string Token, TenantDto Tenant)
    {
        public string? RefreshToken { get; init; }
    }

    public record RegisterTenantDto(string BusinessName, string Email, string Password);

    public record LoginTenantDto(string Email, string Password);

    public record UpdateTenantDto(string BusinessName);

    public record FcmTokenDto(string Token);

    public record RecordPaymentDto(decimal Amount, string Currency, string? Reference);
}

namespace BusinessSearcher.Application.DTOs.StoreManagement
{
    public record AddressDto(
        string  Street,
        string  City,
        string  State,
        string  Country,
        double? Latitude,
        double? Longitude);

    public record StoreDto(
        Guid        Id,
        Guid        TenantId,
        string      Name,
        string?     Description,
        AddressDto  Address,
        string      Phone,
        string?     LogoUrl,
        bool        IsActive,
        bool        IsOpenNow,
        IEnumerable<ScheduleDto>  Schedules);

    public record ScheduleDto(
        Guid   Id,
        string DayOfWeek,
        string OpenTime,
        string CloseTime,
        bool   IsClosed);

    // ── Request DTOs ──
    public record SetupStoreDto(
        string     Name,
        string?    Description,
        AddressDto Address,
        string     Phone,
        string?    LogoUrl);

    public record AddScheduleDto(
        string DayOfWeek,
        string OpenTime,
        string CloseTime,
        bool   IsClosed = false);
}

namespace BusinessSearcher.Application.DTOs.Chat
{
    public record SendMessageDto(string Text);

    public record AdminReplyDto(string Text);

    public record ChatMessageDto(
        Guid     Id,
        string   SenderName,
        string   SenderType,
        string   Text,
        bool     IsRead,
        DateTime SentAt);

    public record ConversationSummaryDto(
        Guid     TenantId,
        string   TenantName,
        string   LastMessage,
        DateTime LastMessageAt,
        bool     LastIsFromAdmin,
        int      UnreadFromTenant);

    public record UnreadCountDto(int Count);
}

namespace BusinessSearcher.Application.DTOs.Admin
{
    public record AdminTenantDto(
        Guid     Id,
        string   BusinessName,
        string   Email,
        string   Status,
        bool     IsEmailVerified,
        bool     IsApproved,
        bool     IsSubscriptionActive,
        int      PaymentsCount,
        decimal  TotalPaid,
        DateTime CreatedAt,
        DateTime? NextPaymentDate,
        string?  PhoneNumber = null,
        DateTime? LastPaymentDate = null);

    public record AdminTenantDetailDto(
        Guid     Id,
        string   BusinessName,
        string   Email,
        string   Status,
        bool     IsEmailVerified,
        bool     IsApproved,
        bool     IsSubscriptionActive,
        decimal  TotalPaid,
        DateTime CreatedAt,
        DateTime? LastPaymentDate,
        DateTime? NextPaymentDate,
        IEnumerable<PaymentRecordDto> Payments,
        string?  PhoneNumber = null);

    public record PaymentRecordDto(
        Guid     Id,
        decimal  Amount,
        string   Currency,
        string?  Reference,
        DateTime PaymentDate);

    public record AdminClientDto(
        Guid     Id,
        string   FullName,
        string   Email,
        string   Plan,
        bool     IsEmailVerified,
        bool     IsApproved,
        int      ReputationScore,
        int      ReportsSubmitted,
        DateTime CreatedAt,
        bool     PremiumRequested = false,
        string?  PhoneNumber = null,
        DateTime? PremiumUntil = null);

    /// <summary>Reporte de pago de Premium (autoservicio del cliente) visto desde el admin, con el estado actual del plan.</summary>
    public record AdminPremiumClaimDto(
        Guid     ClaimId,
        Guid     ClientId,
        string   FullName,
        string   Email,
        string   PhoneNumber,
        decimal  Amount,
        string   Currency,
        string   ProofReference,
        string   ClaimStatus,
        DateTime RequestedAt,
        DateTime? ReviewedAt,
        string   Plan,
        DateTime? PremiumUntil);

    public record AdminReviewPremiumClaimDto(string? Note);

    /// <summary>Cliente Premium cuyo período vence dentro de los próximos N días (aviso previo al corte).</summary>
    public record ExpiringClientDto(
        Guid     ClientId,
        string   FullName,
        string   Email,
        DateTime PremiumUntil,
        int      DaysUntilExpiry);

    public record AdminGlobalStatsDto(
        int     TotalTenants,
        int     TrialTenants,
        int     ActiveTenants,
        int     SuspendedTenants,
        int     InactiveTenants,
        int     NewLast30Days,
        decimal TotalRevenue);

    public record AdminSuspendDto(string? Reason);

    public record AdminRenewDto(decimal Amount, string? Currency, string? Reference);

    /// <summary>Tenant con más de 1 mes calendario sin pagar desde su último pago registrado.</summary>
    public record OverdueTenantDto(
        Guid     TenantId,
        string   BusinessName,
        string   Email,
        string   Status,
        DateTime LastPaymentDate,
        int      DaysOverdue);

    /// <summary>Reporte de pago (autoservicio del tenant) visto desde el admin, con el estado actual de la suscripción.</summary>
    public record AdminPaymentClaimDto(
        Guid     ClaimId,
        Guid     TenantId,
        string   BusinessName,
        string   Email,
        string   PhoneNumber,
        decimal  Amount,
        string   Currency,
        string   ProofReference,
        string   ClaimStatus,
        DateTime RequestedAt,
        DateTime? ReviewedAt,
        string   TenantStatus,
        bool     IsSubscriptionActive,
        DateTime? LastPaymentDate,
        DateTime? NextPaymentDate);

    public record AdminReviewPaymentClaimDto(string? Note);

    /// <summary>Tenant Activo/Trial cuya suscripción vence dentro de los próximos N días (aviso previo al corte).</summary>
    public record ExpiringTenantDto(
        Guid     TenantId,
        string   BusinessName,
        string   Email,
        DateTime NextPaymentDate,
        int      DaysUntilExpiry);
}

namespace BusinessSearcher.Application.DTOs.Common
{
    public record PagedResult<T>(
        IEnumerable<T> Items,
        int TotalCount,
        int Page,
        int PageSize)
    {
        public int  TotalPages  => (int)Math.Ceiling(TotalCount / (double)PageSize);
        public bool HasNextPage => Page < TotalPages;
        public bool HasPrevPage => Page > 1;
    }

    public record SearchProductDto(
        Guid    ProductId,
        string  ProductName,
        string? ProductDescription,
        decimal Price,
        string  Currency,
        string? ImageUrl,
        bool    IsAvailable,
        int     Stock,
        string? CategoryName,
        Guid    StoreId,
        string  StoreName,
        string  StoreAddress,
        string  City,
        double? Latitude,
        double? Longitude,
        string? StorePhone,
        bool    IsStoreOpen,
        string? StoreLogoUrl,
        int     MinOrderQuantity = 1,
        // Origen del resultado: "Store" (negocio/tienda) o "Client" (vendedor particular).
        string  SellerType = "Store");

    public record WholesaleCatalogItemDto(
        Guid    StoreId,
        string  StoreName,
        string  StoreAddress,
        string  City,
        string? StorePhone,
        Guid    ProductId,
        string  ProductName,
        string? ProductDescription,
        decimal Price,
        string  Currency,
        int     Stock,
        int     MinOrderQuantity,
        string? CategoryName);

    public record ImportCatalogResultDto(
        bool     Success,
        int      ImportedCount,
        IReadOnlyList<string> Errors);
}
