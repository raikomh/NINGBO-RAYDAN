using BusinessSearcher.Domain.BoundedContext.Radar.Aggregates;

namespace BusinessSearcher.Domain.BoundedContext.Radar.Repositories
{
    public interface IClientRepository
    {
        Task<Client?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<Client?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
        Task<Client?> GetByRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
        Task<Client?> GetByPasswordResetTokenAsync(string resetToken, CancellationToken cancellationToken = default);
        Task<Client?> GetByEmailVerificationTokenAsync(string token, CancellationToken cancellationToken = default);
        Task<bool>    ExistsEmailAsync(string email, CancellationToken cancellationToken = default);
        Task<Client?> GetByReferralCodeAsync(string referralCode, CancellationToken cancellationToken = default);
        Task<bool>    ExistsReferralCodeAsync(string referralCode, CancellationToken cancellationToken = default);
        Task AddAsync(Client client, CancellationToken cancellationToken = default);
        Task UpdateAsync(Client client, CancellationToken cancellationToken = default);

        /// <summary>Lista paginada para el panel de admin, con filtro opcional de búsqueda y aprobación.</summary>
        Task<(IReadOnlyList<Client> Items, int Total)> GetAllWithFiltersAsync(
            int page, int pageSize, string? search, bool? isApproved,
            CancellationToken cancellationToken = default);
    }

    /// <summary>Proyección para el mapa: un cliente-vendedor con productos disponibles y su ubicación.</summary>
    public record ClientSellerLocation(
        Guid ClientId, string FullName, double Latitude, double Longitude, string? City, int AvailableProducts);

    /// <summary>Resultado de búsqueda de un producto de cliente, con los datos del vendedor unidos.</summary>
    public record ClientProductSearchResult(
        Guid ProductId, string Name, string? Description, decimal Price, string Currency,
        bool IsAvailable, string? ImageUrl,
        Guid SellerId, string SellerName, double? Latitude, double? Longitude, string? City,
        string? SellerPhoneNumber = null, string? SellerStreet = null, string? SellerState = null);

    public interface IClientProductRepository
    {
        Task<ClientProduct?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<ClientProduct>> GetByClientAsync(Guid clientId, CancellationToken cancellationToken = default);
        Task AddAsync(ClientProduct product, CancellationToken cancellationToken = default);
        Task UpdateAsync(ClientProduct product, CancellationToken cancellationToken = default);
        Task DeleteAsync(ClientProduct product, CancellationToken cancellationToken = default);

        /// <summary>Búsqueda cross-cliente (con datos del vendedor) para mezclar con el buscador de tiendas.</summary>
        Task<IReadOnlyList<ClientProductSearchResult>> SearchAsync(
            string? query, string? city, bool? onlyAvailable, decimal? minPrice, decimal? maxPrice,
            int page, int pageSize, CancellationToken cancellationToken = default);

        /// <summary>Clientes con al menos un producto disponible y con ubicación (para el mapa).</summary>
        Task<IReadOnlyList<ClientSellerLocation>> GetAvailableWithLocationAsync(CancellationToken cancellationToken = default);
    }

    public interface IReferralRepository
    {
        Task AddAsync(Referral referral, CancellationToken cancellationToken = default);
        Task<Referral?> GetPendingByInvitedAsync(Guid invitedClientId, CancellationToken cancellationToken = default);
        Task<int> CountValidByInviterAsync(Guid inviterClientId, CancellationToken cancellationToken = default);
        Task UpdateAsync(Referral referral, CancellationToken cancellationToken = default);

        /// <summary>
        /// Conteo de referidos válidos por invitador, filtrados por el mes calendario
        /// (según Referral.ValidatedAt) — base de la competencia mensual. No depende del
        /// contador perezoso Client.ValidReferralsThisMonth, que puede haberse reiniciado ya.
        /// </summary>
        Task<IReadOnlyList<(Guid InviterClientId, int Count, DateTime LastValidatedAt)>> GetValidCountsByInviterForMonthAsync(
            int year, int month, CancellationToken cancellationToken = default);
    }

    public interface IReferralMonthlySettlementRepository
    {
        Task<ReferralMonthlySettlement?> GetByPeriodAsync(int year, int month, CancellationToken cancellationToken = default);
        Task AddAsync(ReferralMonthlySettlement settlement, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Referidos de MiPymes (Tenant referido por el código de un Client). Solo guarda el
    /// Guid del Tenant referido — sin navegación cruzada, Tenant vive en otro bounded context.
    /// </summary>
    public interface IMipymeReferralRepository
    {
        Task AddAsync(MipymeReferral referral, CancellationToken cancellationToken = default);
        Task<MipymeReferral?> GetPendingByReferredTenantAsync(Guid referredTenantId, CancellationToken cancellationToken = default);
        Task UpdateAsync(MipymeReferral referral, CancellationToken cancellationToken = default);

        /// <summary>
        /// Conteo de MiPymes válidas por invitador (Client), filtradas por el mes calendario
        /// (según MipymeReferral.ValidatedAt) — base de la competencia mensual de MiPymes.
        /// </summary>
        Task<IReadOnlyList<(Guid ReferrerClientId, int Count, DateTime LastValidatedAt)>> GetValidCountsByReferrerForMonthAsync(
            int year, int month, CancellationToken cancellationToken = default);
    }

    public interface IMipymeReferralMonthlySettlementRepository
    {
        Task<MipymeReferralMonthlySettlement?> GetByPeriodAsync(int year, int month, CancellationToken cancellationToken = default);
        Task AddAsync(MipymeReferralMonthlySettlement settlement, CancellationToken cancellationToken = default);
    }

    public interface IAvailabilityReportRepository
    {
        Task<AvailabilityReport?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task AddAsync(AvailabilityReport report, CancellationToken cancellationToken = default);
        Task UpdateAsync(AvailabilityReport report, CancellationToken cancellationToken = default);

        /// <summary>
        /// Reportes recientes (dentro de <paramref name="freshnessMinutes"/>) que coinciden con los
        /// filtros. La distancia se aplica en memoria por Haversine sobre los candidatos.
        /// </summary>
        Task<IReadOnlyList<AvailabilityReport>> GetRecentAsync(
            string? productQuery,
            string? city,
            int freshnessMinutes,
            CancellationToken cancellationToken = default);
    }

    public interface IAvailabilityAlertRepository
    {
        Task<AvailabilityAlert?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<AvailabilityAlert>> GetByClientAsync(Guid clientId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<AvailabilityAlert>> GetActiveAsync(CancellationToken cancellationToken = default);
        Task AddAsync(AvailabilityAlert alert, CancellationToken cancellationToken = default);
        Task UpdateAsync(AvailabilityAlert alert, CancellationToken cancellationToken = default);
    }

    public interface ICommunityQuestionRepository
    {
        Task<CommunityQuestion?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<CommunityQuestion>> GetRecentAsync(string? city, int limit, CancellationToken cancellationToken = default);
        Task AddAsync(CommunityQuestion question, CancellationToken cancellationToken = default);
        Task UpdateAsync(CommunityQuestion question, CancellationToken cancellationToken = default);
    }
}
