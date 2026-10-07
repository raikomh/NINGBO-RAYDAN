using BusinessSearcher.Application.Commons.Interfaces;
using BusinessSearcher.Application.DTOs.Radar;
using BusinessSearcher.Application.Features.StoreManagement.Queries.NearbyStores;
using BusinessSearcher.Domain.BoundedContext.Radar.Aggregates;
using BusinessSearcher.Domain.BoundedContext.Radar.Enums;
using BusinessSearcher.Domain.BoundedContext.Radar.Repositories;
using BusinessSearcher.Domain.BoundedContext.Radar.ValueObjects;
using BusinessSearcher.Domain.BoundedContext.StoreManagement.Repositories;
using BusinessSearcher.Domain.Exceptions;
using MediatR;

namespace BusinessSearcher.Application.Features.Radar.Ai
{
    /// <summary>Guard Premium compartido y prompt del asistente de comida.</summary>
    internal static class AiShared
    {
        public const string FoodSystemPrompt =
            "Eres el asistente de FoodFinder, una app cubana para encontrar disponibilidad de " +
            "alimentos. Ayudas ÚNICAMENTE con temas de comida y abastecimiento: dónde y cuándo " +
            "conseguir alimentos, precios, sustitutos de ingredientes, recetas sencillas con lo " +
            "disponible, planificación de compras y consejos para ahorrar en el contexto cubano. " +
            "Si te preguntan algo que no es sobre comida o abastecimiento, decláralo con amabilidad " +
            "y reconduce la conversación a la comida. Responde en español, breve y concreto.";

        public static async Task<Client> RequirePremiumAsync(
            IClientRepository clients, ICurrentUserService currentUser, CancellationToken ct)
        {
            var client = await clients.GetByIdAsync(currentUser.AccountId, ct)
                ?? throw new DomainException("Cliente no encontrado.");
            if (client.Plan != ClientPlan.Premium)
                throw new DomainException("El asistente inteligente es una función Premium.");
            return client;
        }
    }
}

namespace BusinessSearcher.Application.Features.Radar.Commands.FoodAssistantChat
{
    using BusinessSearcher.Application.Features.Radar.Ai;

    public record FoodAssistantChatCommand(IReadOnlyList<AiChatTurnDto> Messages) : IRequest<AiChatReplyDto>;

    public class FoodAssistantChatCommandHandler : IRequestHandler<FoodAssistantChatCommand, AiChatReplyDto>
    {
        private readonly IAiChatService      _ai;
        private readonly IClientRepository   _clients;
        private readonly ICurrentUserService _currentUser;

        public FoodAssistantChatCommandHandler(
            IAiChatService ai, IClientRepository clients, ICurrentUserService currentUser)
        {
            _ai = ai; _clients = clients; _currentUser = currentUser;
        }

        public async Task<AiChatReplyDto> Handle(FoodAssistantChatCommand request, CancellationToken ct)
        {
            await AiShared.RequirePremiumAsync(_clients, _currentUser, ct);

            var turns = (request.Messages ?? new List<AiChatTurnDto>())
                .Where(m => !string.IsNullOrWhiteSpace(m.Content))
                .Select(m => new AiMessage(
                    m.Role?.ToLowerInvariant() == "assistant" ? "assistant" : "user",
                    m.Content.Trim()))
                .ToList();

            if (turns.Count == 0)
                throw new DomainException("Escribe un mensaje para el asistente.");

            var reply = await _ai.CompleteAsync(AiShared.FoodSystemPrompt, turns, ct);
            return new AiChatReplyDto(reply);
        }
    }
}

namespace BusinessSearcher.Application.Features.Radar.Queries.SmartMarket
{
    using BusinessSearcher.Application.Features.Radar.Ai;

    /// <summary>
    /// "Mercado Inteligente": en vez de que el usuario busque, se le arma un resumen
    /// personalizado (probabilidad de encontrar cada favorito hoy, precio más barato,
    /// mejor lugar y distancia) a partir de los reportes recientes del radar.
    /// </summary>
    public record GetSmartMarketQuery(
        double        Latitude,
        double        Longitude,
        double        RadiusKm,
        List<string>  Favorites,
        decimal?      Budget) : IRequest<SmartMarketDto>;

    public class GetSmartMarketQueryHandler : IRequestHandler<GetSmartMarketQuery, SmartMarketDto>
    {
        private const int FreshnessMinutes = 4320; // 3 días

        private readonly IAvailabilityReportRepository _reports;
        private readonly IClientRepository             _clients;
        private readonly ICurrentUserService           _currentUser;
        private readonly IAiChatService                _ai;
        private readonly IDateTimeService              _clock;

        public GetSmartMarketQueryHandler(
            IAvailabilityReportRepository reports, IClientRepository clients,
            ICurrentUserService currentUser, IAiChatService ai, IDateTimeService clock)
        {
            _reports = reports; _clients = clients; _currentUser = currentUser; _ai = ai; _clock = clock;
        }

        public async Task<SmartMarketDto> Handle(GetSmartMarketQuery request, CancellationToken ct)
        {
            await AiShared.RequirePremiumAsync(_clients, _currentUser, ct);

            var now    = _clock.UtcNow;
            var origin = new GeoLocation(request.Latitude, request.Longitude);
            var radius = request.RadiusKm <= 0 ? 5 : request.RadiusKm;
            var favs   = (request.Favorites ?? new List<string>())
                .Where(f => !string.IsNullOrWhiteSpace(f))
                .Select(f => f.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var items = new List<SmartMarketItemDto>();
            foreach (var fav in favs)
            {
                var recent = await _reports.GetRecentAsync(fav, null, FreshnessMinutes, ct);
                var within = recent
                    .Select(r => (Report: r, Distance: origin.DistanceKmTo(r.Location)))
                    .Where(x => x.Distance <= radius)
                    .ToList();

                if (within.Count == 0)
                {
                    items.Add(new SmartMarketItemDto(fav, 0, null, null, null, null, 0));
                    continue;
                }

                var available = within
                    .Where(x => x.Report.Status == AvailabilityStatus.Available && !x.Report.IsLikelyDepleted(now))
                    .ToList();

                var withPrice = available
                    .Where(x => x.Report.Price.HasValue &&
                                (!request.Budget.HasValue || x.Report.Price!.Value <= request.Budget.Value))
                    .OrderBy(x => x.Report.Price!.Value)
                    .ToList();

                var cheapest = withPrice.Count > 0 ? withPrice[0].Report : null;
                var best     = available.OrderByDescending(x => x.Report.LastActivityAt).FirstOrDefault();
                var pct      = (int)Math.Round(100.0 * available.Count / within.Count);

                items.Add(new SmartMarketItemDto(
                    fav,
                    pct,
                    cheapest?.Price,
                    cheapest?.Currency,
                    best.Report?.PlaceName,
                    available.Count > 0 ? Math.Round(available.Min(x => x.Distance), 2) : null,
                    within.Count));
            }

            var summary = await BuildSummaryAsync(items, request.Budget, ct);
            return new SmartMarketDto(summary, items, now);
        }

        private async Task<string> BuildSummaryAsync(
            List<SmartMarketItemDto> items, decimal? budget, CancellationToken ct)
        {
            if (items.Count == 0)
                return "Agrega productos favoritos para recibir tu resumen diario personalizado.";

            // Resumen determinista (siempre disponible, aunque la IA no esté configurada).
            var lines = items.Select(i => i.RecentReports == 0
                ? $"- {i.Product}: sin reportes recientes cerca."
                : $"- {i.Product}: {i.AvailabilityPercent}% disponible" +
                  (i.CheapestPrice.HasValue ? $", desde {i.CheapestPrice} {i.Currency}" : "") +
                  (i.BestPlace is not null ? $", mejor en {i.BestPlace}" : "") +
                  (i.NearestKm.HasValue ? $" (a {i.NearestKm} km)" : "") + ".");
            var deterministic = "Tu mercado inteligente de hoy:\n" + string.Join("\n", lines);

            if (!_ai.IsConfigured)
                return deterministic;

            // Narrativa amable con la IA a partir de los datos ya calculados.
            var budgetLine = budget.HasValue ? $" El presupuesto del usuario es {budget}." : "";
            var prompt = new AiMessage("user",
                "Con estos datos de disponibilidad de alimentos cerca del usuario, redacta un " +
                "resumen breve, cálido y accionable (máx. 5 frases) en español, priorizando lo más " +
                "probable y barato." + budgetLine + "\n\n" + deterministic);

            var narrative = await _ai.CompleteAsync(AiShared.FoodSystemPrompt, new[] { prompt }, ct);
            return string.IsNullOrWhiteSpace(narrative) ? deterministic : narrative;
        }
    }
}

namespace BusinessSearcher.Application.Features.Radar.Queries.BudgetRecommendation
{
    using BusinessSearcher.Application.Features.Radar.Ai;

    /// <summary>
    /// El bot recomienda, con un presupuesto dado, qué comida comprar entre lo
    /// disponible en las tiendas cercanas, y devuelve cada producto ubicado
    /// (tienda + coordenadas) para pintarlo en el mapa.
    /// </summary>
    public record GetBudgetRecommendationQuery(
        double  Latitude,
        double  Longitude,
        double  RadiusKm,
        decimal Budget) : IRequest<BudgetRecommendationDto>;

    public class GetBudgetRecommendationQueryHandler
        : IRequestHandler<GetBudgetRecommendationQuery, BudgetRecommendationDto>
    {
        private readonly ISearchRepository   _search;
        private readonly IClientRepository   _clients;
        private readonly ICurrentUserService _currentUser;
        private readonly IAiChatService      _ai;
        private readonly IDateTimeService    _clock;

        public GetBudgetRecommendationQueryHandler(
            ISearchRepository search, IClientRepository clients,
            ICurrentUserService currentUser, IAiChatService ai, IDateTimeService clock)
        {
            _search = search; _clients = clients; _currentUser = currentUser; _ai = ai; _clock = clock;
        }

        public async Task<BudgetRecommendationDto> Handle(GetBudgetRecommendationQuery request, CancellationToken ct)
        {
            await AiShared.RequirePremiumAsync(_clients, _currentUser, ct);

            if (request.Budget <= 0)
                throw new DomainException("Indica un presupuesto mayor que cero.");

            var radius = request.RadiusKm <= 0 ? 5 : request.RadiusKm;

            // Productos disponibles y dentro del presupuesto (precio unitario <= budget).
            var products = await _search.SearchProductsAsync(
                query: null, city: null, categoryId: null, onlyAvailable: true,
                minPrice: null, maxPrice: request.Budget, storeId: null, page: 1, pageSize: 300, cancellationToken: ct);

            var options = products
                .Select(p => new
                {
                    P = p,
                    Dist = (p.Latitude.HasValue && p.Longitude.HasValue)
                        ? GeoMath.HaversineKm(request.Latitude, request.Longitude, p.Latitude.Value, p.Longitude.Value)
                        : (double?)null
                })
                .Where(x => x.Dist is null || x.Dist <= radius)
                .OrderBy(x => x.P.Price)
                .ThenBy(x => x.Dist ?? double.MaxValue)
                .Select(x => new NearbyProductDto(
                    x.P.ProductName, x.P.Price, x.P.Currency, x.P.StoreName, x.P.StoreAddress, x.P.City,
                    x.P.Latitude, x.P.Longitude, x.Dist.HasValue ? Math.Round(x.Dist.Value, 2) : null))
                .ToList();

            var recommendation = await BuildRecommendationAsync(request.Budget, options, ct);
            return new BudgetRecommendationDto(recommendation, request.Budget, options, _clock.UtcNow);
        }

        private async Task<string> BuildRecommendationAsync(
            decimal budget, List<NearbyProductDto> options, CancellationToken ct)
        {
            if (options.Count == 0)
                return $"No encontré productos disponibles dentro de {budget} cerca de ti. " +
                       "Prueba ampliar el radio o subir el presupuesto.";

            var lines = options
                .Take(25)
                .Select(o => $"- {o.Product}: {o.Price} {o.Currency} en {o.StoreName}" +
                             (o.DistanceKm.HasValue ? $" (a {o.DistanceKm} km)" : ""));
            var catalog = string.Join("\n", lines);

            if (!_ai.IsConfigured)
                return $"Con {budget} puedes elegir entre estos productos disponibles cerca:\n{catalog}";

            var prompt = new AiMessage("user",
                $"El usuario tiene un presupuesto de {budget}. Con estos productos disponibles cerca " +
                "(nombre: precio en tienda), recomiéndale qué comprar para comer bien sin pasarse del " +
                "presupuesto: sugiere una combinación concreta (con el total aproximado) y una comida " +
                "sencilla que pueda preparar. Prioriza lo más barato y cercano. Responde breve en español.\n\n" +
                catalog);

            var narrative = await _ai.CompleteAsync(AiShared.FoodSystemPrompt, new[] { prompt }, ct);
            return string.IsNullOrWhiteSpace(narrative)
                ? $"Con {budget} puedes elegir entre:\n{catalog}"
                : narrative;
        }
    }
}

namespace BusinessSearcher.Application.Features.Radar.Queries.SupplyPrediction
{
    using BusinessSearcher.Application.Features.Radar.Ai;

    /// <summary>
    /// Predicción heurística de abastecimiento a partir del histórico de reportes
    /// (últimos 14 días): mejores lugares y franja horaria donde suele aparecer el producto.
    /// No es un modelo entrenado; es una señal basada en la actividad de la comunidad.
    /// </summary>
    public record GetSupplyPredictionQuery(string Product, string? City) : IRequest<SupplyPredictionDto>;

    public class GetSupplyPredictionQueryHandler : IRequestHandler<GetSupplyPredictionQuery, SupplyPredictionDto>
    {
        private const int FreshnessMinutes = 20160; // 14 días

        private readonly IAvailabilityReportRepository _reports;
        private readonly IClientRepository             _clients;
        private readonly ICurrentUserService           _currentUser;

        public GetSupplyPredictionQueryHandler(
            IAvailabilityReportRepository reports, IClientRepository clients, ICurrentUserService currentUser)
        {
            _reports = reports; _clients = clients; _currentUser = currentUser;
        }

        public async Task<SupplyPredictionDto> Handle(GetSupplyPredictionQuery request, CancellationToken ct)
        {
            await AiShared.RequirePremiumAsync(_clients, _currentUser, ct);

            if (string.IsNullOrWhiteSpace(request.Product))
                throw new DomainException("Indica el producto a predecir.");

            var recent    = await _reports.GetRecentAsync(request.Product, request.City, FreshnessMinutes, ct);
            var available = recent.Where(r => r.Status == AvailabilityStatus.Available).ToList();
            var sample    = recent.Count;

            var bestPlaces = available
                .GroupBy(r => r.PlaceName, StringComparer.OrdinalIgnoreCase)
                .Where(g => !string.IsNullOrWhiteSpace(g.Key))
                .OrderByDescending(g => g.Count())
                .Take(3)
                .Select(g => g.Key)
                .ToList();

            string? bestTime = available.Count == 0 ? null : available
                .GroupBy(r => Franja(r.ReportedAt.Hour))
                .OrderByDescending(g => g.Count())
                .First().Key;

            var assessment = sample == 0
                ? $"Aún no hay reportes recientes de {request.Product}" +
                  (request.City is not null ? $" en {request.City}" : "") + ". Sé el primero en reportar."
                : available.Count == 0
                    ? $"{request.Product} se ha reportado, pero casi siempre agotado últimamente. Baja probabilidad hoy."
                    : $"{request.Product} apareció disponible en {available.Count} de {sample} reportes recientes" +
                      (bestTime is not null ? $", sobre todo por la {bestTime}" : "") + ".";

            return new SupplyPredictionDto(request.Product, assessment, bestPlaces, bestTime, sample);
        }

        private static string Franja(int hour) => hour switch
        {
            >= 6 and < 12  => "mañana",
            >= 12 and < 18 => "tarde",
            >= 18 and < 24 => "noche",
            _              => "madrugada"
        };
    }
}

namespace BusinessSearcher.Application.Features.Radar.Queries.RecipePlanner
{
    using BusinessSearcher.Application.Features.Radar.Ai;

    /// <summary>
    /// Asistente de recetas (visión v2): el usuario dice qué quiere cocinar y la IA
    /// descompone el plato en ingredientes, busca cuáles están disponibles cerca (con
    /// precio y ubicación) y cuáles faltan, y arma una recomendación.
    /// </summary>
    public record PlanRecipeQuery(
        string Dish, int People, double Latitude, double Longitude, double RadiusKm, decimal? Budget)
        : IRequest<RecipePlanDto>;

    public class PlanRecipeQueryHandler : IRequestHandler<PlanRecipeQuery, RecipePlanDto>
    {
        private readonly ISearchRepository   _search;
        private readonly IClientRepository   _clients;
        private readonly ICurrentUserService _currentUser;
        private readonly IAiChatService      _ai;

        public PlanRecipeQueryHandler(
            ISearchRepository search, IClientRepository clients,
            ICurrentUserService currentUser, IAiChatService ai)
        {
            _search = search; _clients = clients; _currentUser = currentUser; _ai = ai;
        }

        public async Task<RecipePlanDto> Handle(PlanRecipeQuery request, CancellationToken ct)
        {
            await AiShared.RequirePremiumAsync(_clients, _currentUser, ct);

            if (string.IsNullOrWhiteSpace(request.Dish))
                throw new DomainException("Indica qué quieres cocinar.");

            var radius      = request.RadiusKm <= 0 ? 10 : request.RadiusKm;
            var people      = request.People <= 0 ? 2 : request.People;
            var ingredients = await GetIngredientsAsync(request.Dish, people, ct);

            var ingredientResults = new List<RecipeIngredientDto>();
            var missing = new List<string>();

            foreach (var ing in ingredients)
            {
                var products = await _search.SearchProductsAsync(
                    query: ing, city: null, categoryId: null, onlyAvailable: true,
                    minPrice: null, maxPrice: request.Budget, storeId: null, page: 1, pageSize: 50, cancellationToken: ct);

                var variants = products
                    .Select(p => new
                    {
                        P = p,
                        Dist = (p.Latitude.HasValue && p.Longitude.HasValue)
                            ? GeoMath.HaversineKm(request.Latitude, request.Longitude, p.Latitude.Value, p.Longitude.Value)
                            : (double?)null
                    })
                    .Where(x => x.Dist is null || x.Dist <= radius)
                    // Una entrada por producto+tienda; hasta 4 variantes, más barata/cercana primero.
                    .GroupBy(x => (x.P.ProductName, x.P.StoreName))
                    .Select(g => g.OrderBy(x => x.P.Price).ThenBy(x => x.Dist ?? double.MaxValue).First())
                    .OrderBy(x => x.P.Price)
                    .ThenBy(x => x.Dist ?? double.MaxValue)
                    .Take(4)
                    .Select(x => new NearbyProductDto(
                        x.P.ProductName, x.P.Price, x.P.Currency, x.P.StoreName, x.P.StoreAddress, x.P.City,
                        x.P.Latitude, x.P.Longitude, x.Dist.HasValue ? Math.Round(x.Dist.Value, 2) : null))
                    .ToList();

                if (variants.Count == 0) missing.Add(ing);
                else ingredientResults.Add(new RecipeIngredientDto(ing, variants));
            }

            var recommendation = await BuildRecommendationAsync(request.Dish, people, ingredientResults, missing, ct);
            return new RecipePlanDto(recommendation, ingredientResults, missing);
        }

        private async Task<List<string>> GetIngredientsAsync(string dish, int people, CancellationToken ct)
        {
            if (!_ai.IsConfigured)
                return new List<string> { dish };

            var prompt = new AiMessage("user",
                $"Lista SOLO los ingredientes principales (máximo 8) para preparar «{dish}» para {people} personas. " +
                "Responde únicamente los ingredientes en una sola línea separados por comas, sin cantidades ni texto extra.");

            var reply = await _ai.CompleteAsync(AiShared.FoodSystemPrompt, new[] { prompt }, ct);

            var list = reply
                .Split(new[] { ',', '\n', '·', '-' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(s => s.Length is > 1 and < 40)
                .Take(8)
                .ToList();

            return list.Count > 0 ? list : new List<string> { dish };
        }

        private async Task<string> BuildRecommendationAsync(
            string dish, int people, List<RecipeIngredientDto> found, List<string> missing, CancellationToken ct)
        {
            var foundLine   = found.Count == 0 ? "nada por ahora" :
                string.Join("; ", found.Select(fi =>
                    $"{fi.Ingredient}: " + string.Join(", ",
                        fi.Variants.Select(v => $"{v.Product} ({v.Price:0} {v.Currency} en {v.StoreName})"))));
            var missingLine = missing.Count == 0 ? "ninguno" : string.Join(", ", missing);
            var deterministic =
                $"Para {dish} ({people} personas):\n✅ Disponible cerca: {foundLine}\n🛒 Te falta conseguir: {missingLine}";

            if (!_ai.IsConfigured) return deterministic;

            var prompt = new AiMessage("user",
                $"El usuario quiere preparar «{dish}» para {people} personas. Disponible cerca (por ingrediente, con sus variantes y tiendas): {foundLine}. " +
                $"Le falta: {missingLine}. Redacta una recomendación breve y útil (máx. 4 frases) en español: con qué " +
                "variante quedarse y en qué tienda, con qué sustituir lo que falta y un consejo de preparación.");

            var narrative = await _ai.CompleteAsync(AiShared.FoodSystemPrompt, new[] { prompt }, ct);
            return string.IsNullOrWhiteSpace(narrative) ? deterministic : narrative;
        }
    }
}
