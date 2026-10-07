namespace BusinessSearcher.Domain.BoundedContext.TenantManagement.Enums
{
    public enum TenantStatus
    {
        Active    = 1,
        Inactive  = 2,
        Trial     = 3,
        Suspended = 4,
        Deleted   = 5
    }

    public enum TenantType
    {
        Wholesale = 1,
        Retail    = 2
    }

    public enum SyncDirection
    {
        Push = 1,
        // Reservado para cuando exista sincronización online → local (pull); no se usa todavía.
        Pull = 2
    }

    public enum SyncStatus
    {
        InProgress     = 1,
        Success        = 2,
        PartialFailure = 3,
        Failed         = 4
    }

    public enum PaymentClaimStatus
    {
        Pending  = 1,
        Approved = 2,
        Rejected = 3
    }
}
