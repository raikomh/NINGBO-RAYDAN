namespace BusinessSearcher.Application.DTOs.Radar
{
    public record ClientDto(
        Guid     Id,
        string   FullName,
        string   Email,
        string   Plan,
        bool     IsEmailVerified,
        int      ReputationScore,
        int      ReportsSubmitted,
        DateTime CreatedAt,
        bool     PremiumRequested = false,
        decimal  ReferralCupBalance = 0m,
        string?  PhoneNumber = null,
        DateTime? PremiumUntil = null,
        string?  Street  = null,
        string?  City    = null,
        string?  State   = null,
        string?  Country = null,
        double?  Latitude  = null,
        double?  Longitude = null);

    /// <summary>Perfil público de un cliente-vendedor visible para otros clientes (nunca expone email/password/plan).</summary>
    public record SellerPublicProfileDto(
        string   FullName,
        string?  PhoneNumber,
        string?  Street,
        string?  City,
        string?  State,
        string?  Country,
        double?  Latitude,
        double?  Longitude);

    /// <summary>
    /// Payload para que el cliente edite su propio perfil desde la app: nombre,
    /// teléfono y dirección/ubicación. Todo es opcional y null significa "no tocar";
    /// FullName y PhoneNumber en blanco también se ignoran (el dominio los exige
    /// no vacíos), mientras que una dirección en blanco sí limpia el campo.
    /// </summary>
    public record UpdateClientProfileDto(
        string? Street    = null,
        string? City      = null,
        string? State     = null,
        string? Country   = null,
        double? Latitude  = null,
        double? Longitude = null,
        string? FullName    = null,
        string? PhoneNumber = null);

    public record ClientPremiumClaimDto(
        Guid     Id,
        decimal  Amount,
        string   Currency,
        string   ProofReference,
        string   PhoneNumber,
        string   Status,
        DateTime RequestedAt,
        DateTime? ReviewedAt,
        string?  ReviewNote);

    // Token es null cuando el registro deja la cuenta pendiente de aprobación de admin
    // (todavía no hay sesión: el cliente debe esperar a ser aprobado para hacer login).
    public record ClientAuthResultDto(string? Token, ClientDto Client)
    {
        public string? RefreshToken { get; init; }
    }

    // ── Request DTOs (auth) ──
    public record RegisterClientDto(string FullName, string Email, string Password, string? ReferralCode = null, string? PhoneNumber = null);
    public record LoginClientDto(string Email, string Password);
    public record ClientFcmTokenDto(string Token);
    public record SubmitPremiumClaimDto(string PhoneNumber, decimal Amount, string? Currency, string ProofReference);

    // ── Productos del cliente-vendedor ──
    public record ClientProductDto(
        Guid     Id,
        string   Name,
        string?  Description,
        decimal  Price,
        string   Currency,
        bool     IsAvailable,
        string?  ImageUrl,
        DateTime CreatedAt);

    public record CreateClientProductDto(
        string   Name,
        decimal  Price,
        string?  Currency = null,
        string?  Description = null,
        string?  ImageUrl = null,
        double?  Latitude = null,
        double?  Longitude = null,
        string?  City = null);

    public record UpdateClientProductDto(
        string   Name,
        decimal  Price,
        string?  Currency = null,
        string?  Description = null,
        string?  ImageUrl = null);

    public record SetClientProductAvailabilityDto(bool IsAvailable);

    // ── Mapa combinado (tiendas mayoristas/minoristas + clientes-vendedores) ──
    public record NearbyMapEntryDto(
        string   Type,        // "Wholesale" | "Retail" | "Client"
        Guid     Id,
        string   Name,
        double   Latitude,
        double   Longitude,
        double?  DistanceKm,
        int      AvailableProducts,
        string?  Address,
        string?  Phone);

    // ── Referidos ──
    public record ReferralTierDto(int Threshold, int PremiumDays, bool Reached);
    public record MyReferralsDto(
        string                       ReferralCode,
        int                          ValidReferralsTotal,
        int                          ValidReferralsThisMonth,
        int?                         NextThreshold,
        int                          MissingForNext,
        string?                      PremiumUntil,
        IReadOnlyList<ReferralTierDto> Tiers,
        decimal                      CupBalance = 0m,
        decimal                      CupPaidTotal = 0m);

    // ── Competencia mensual de referidos ──
    public record ReferralLeaderboardEntryDto(int Rank, string Name, int ValidReferralsThisMonth, bool IsMe);
    public record ReferralLeaderboardDto(
        int                                     Year,
        int                                     Month,
        IReadOnlyList<ReferralLeaderboardEntryDto> Top,
        int?                                    MyRank,
        int                                     MyCount);

    public record MonthlySettlementReceiptDto(
        int      Year,
        int      Month,
        Guid?    WinnerClientId,
        string?  WinnerName,
        decimal  PrizeAmount,
        int      TotalValidReferrals,
        bool     AlreadySettled);

    // ── Admin: saldos de referidos pendientes de pago ──
    public record ReferralPayoutAdminDto(
        Guid    ClientId,
        string  FullName,
        string  Email,
        int     ValidReferralsTotal,
        decimal ReferralBonusTotal,
        decimal CompetitionPrizeTotal,
        decimal CupBalance,
        decimal CupPaidTotal,
        int     MipymeReferralsTotal = 0,
        decimal MipymeBonusTotal = 0m,
        decimal MipymePrizeTotal = 0m);

    // ── Referidos de MiPymes (competencia paralela, mismo saldo unificado de CUP) ──
    public record MyMipymeReferralsDto(int MipymeReferralsTotal, int MipymeReferralsThisMonth);

    public record MipymeReferralLeaderboardEntryDto(int Rank, string Name, int MipymeReferralsThisMonth, bool IsMe);
    public record MipymeReferralLeaderboardDto(
        int                                            Year,
        int                                            Month,
        IReadOnlyList<MipymeReferralLeaderboardEntryDto> Top,
        int?                                           MyRank,
        int                                            MyCount);

    public record MipymeMonthlySettlementReceiptDto(
        int      Year,
        int      Month,
        Guid?    WinnerClientId,
        string?  WinnerName,
        decimal  PrizeAmount,
        int      TotalValidMipymeReferrals,
        bool     AlreadySettled);

    // ── Reportes de disponibilidad ──
    public record ReportDto(
        Guid     Id,
        string   ProductName,
        string   Status,
        double   Latitude,
        double   Longitude,
        string   City,
        string?  Municipality,
        string   PlaceName,
        Guid?    StoreId,
        decimal? Price,
        string?  Currency,
        string?  PhotoUrl,
        int      Confidence,
        int      PositiveConfirmations,
        int      NegativeConfirmations,
        bool     IsLikelyDepleted,
        Guid     ReporterClientId,
        DateTime ReportedAt,
        DateTime LastActivityAt,
        double?  DistanceKm = null,
        int      EstimatedMinutesLeft = 0,
        string   QueueStatus = "Unknown",
        string   Source = "Community");

    public record CreateReportDto(
        string   ProductName,
        string   Status,          // Available | LowStock | OutOfStock
        double   Latitude,
        double   Longitude,
        string   City,
        string   PlaceName,
        Guid?    StoreId       = null,
        string?  Municipality  = null,
        decimal? Price         = null,
        string?  Currency      = null,
        string?  PhotoUrl      = null,
        string?  QueueStatus   = null);   // NoQueue | Short | Long

    public record ConfirmReportDto(
        bool    Agrees,
        string? ReportedStatus = null);   // Available | LowStock | OutOfStock

    public record AttachReportPhotoDto(string PhotoUrl);

    // ── Alertas de radar (solo Premium) ──
    public record AlertDto(
        Guid      Id,
        string    ProductNameFilter,
        double?   CenterLatitude,
        double?   CenterLongitude,
        double?   RadiusKm,
        decimal?  MaxPrice,
        string?   City,
        bool      IsActive,
        DateTime? LastTriggeredAt,
        DateTime  CreatedAt);

    public record CreateAlertDto(
        string   ProductName,
        double?  Latitude  = null,
        double?  Longitude = null,
        double?  RadiusKm  = null,
        decimal? MaxPrice  = null,
        string?  City      = null);

    // ── IA (Fase 4, solo Premium): asistente de comida, mercado inteligente, predicción ──
    public record AiChatTurnDto(string Role, string Content);   // Role: "user" | "assistant"

    public record AiChatRequestDto(List<AiChatTurnDto> Messages);

    public record AiChatReplyDto(string Reply);

    public record SmartMarketItemDto(
        string   Product,
        int      AvailabilityPercent,
        decimal? CheapestPrice,
        string?  Currency,
        string?  BestPlace,
        double?  NearestKm,
        int      RecentReports);

    public record SmartMarketDto(
        string                       Summary,
        IReadOnlyList<SmartMarketItemDto> Items,
        DateTime                     GeneratedAt);

    public record SupplyPredictionDto(
        string                Product,
        string                Assessment,
        IReadOnlyList<string> BestPlaces,
        string?               BestTimeOfDay,
        int                   SampleSize);

    // ── Recomendación por presupuesto (IA + catálogo de tiendas cercanas) ──
    public record NearbyProductDto(
        string   Product,
        decimal  Price,
        string   Currency,
        string   StoreName,
        string   StoreAddress,
        string   City,
        double?  Latitude,
        double?  Longitude,
        double?  DistanceKm);

    public record BudgetRecommendationDto(
        string                          Recommendation,
        decimal                         Budget,
        IReadOnlyList<NearbyProductDto> Options,
        DateTime                        GeneratedAt);

    // ── Inteligencia del radar (agregaciones sobre reportes) ──
    public record AvailabilityByDayDto(string Day, int AvailableReports, int TotalReports, string? BestHourRange);
    public record AvailabilityPatternDto(string Product, IReadOnlyList<AvailabilityByDayDto> ByDay, string Summary);

    public record PlaceRankingDto(
        string PlaceName, string City, int Reports, int AvailabilityPercent,
        int AvgConfidence, DateTime LastSeen, double? DistanceKm);

    public record TrendItemDto(string Product, int Reports);
    public record TrendPlaceDto(string PlaceName, string City, int Reports);
    public record RadarTrendsDto(
        IReadOnlyList<TrendItemDto>  TopProducts,
        IReadOnlyList<ReportDto>     RecentlyReported,
        IReadOnlyList<TrendPlaceDto> ActivePlaces,
        DateTime                     GeneratedAt);

    public record HeatmapCellDto(string Area, int Reports, int AvailablePercent);
    public record RadarHeatmapDto(string? Product, IReadOnlyList<HeatmapCellDto> Cells, DateTime GeneratedAt);

    public record RouteStopDto(
        string Product, string PlaceName, double Latitude, double Longitude,
        decimal? Price, string? Currency, double LegDistanceKm);
    public record RadarRouteDto(IReadOnlyList<RouteStopDto> Stops, double TotalDistanceKm, IReadOnlyList<string> NotFound);

    // ── Asistente de recetas (v2 IA) ──
    // Cada ingrediente trae varias VARIANTES reales encontradas en tiendas
    // (p. ej. "queso" → Queso Gouda en TiendaPrueba, Queso Crema en Mercado 23).
    public record RecipeIngredientDto(
        string                          Ingredient,
        IReadOnlyList<NearbyProductDto> Variants);

    public record RecipePlanDto(
        string                             Recommendation,
        IReadOnlyList<RecipeIngredientDto> Ingredients,
        IReadOnlyList<string>              Missing);

    // ── Planificador familiar semanal (#8) ──
    public record WeeklyPlanDto(
        string                          Recommendation,
        IReadOnlyList<NearbyProductDto> ShoppingList,
        IReadOnlyList<string>           Missing,
        decimal                         EstimatedTotal,
        decimal                         Budget,
        bool                            WithinBudget,
        int                             People);

    // ── Comunidad (feature 15) ──
    public record CommunityAnswerDto(Guid Id, string AnswererName, string Text, DateTime CreatedAt);
    public record CommunityQuestionDto(
        Guid     Id,
        string   AskerName,
        string   Text,
        string?  City,
        string?  ProductName,
        DateTime CreatedAt,
        IReadOnlyList<CommunityAnswerDto> Answers);

    public record AskQuestionDto(string Text, string? City = null, string? ProductName = null);
    public record AnswerQuestionDto(string Text);
}
