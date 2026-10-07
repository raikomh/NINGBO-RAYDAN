using BusinessSearcher.Application.Commons.Interfaces;
using BusinessSearcher.Application.DTOs.Radar;
using BusinessSearcher.Application.Features.StoreManagement.Queries.NearbyStores;
using BusinessSearcher.Domain.BoundedContext.Radar.Repositories;
using BusinessSearcher.Domain.BoundedContext.StoreManagement.Repositories;
using BusinessSearcher.Domain.Exceptions;
using MediatR;

namespace BusinessSearcher.Application.Features.Radar.Queries.WeeklyPlan
{
    using BusinessSearcher.Application.Features.Radar.Ai;

    /// <summary>
    /// Planificador familiar semanal (feature 8): dado el número de personas y un
    /// presupuesto, la IA arma una lista de la compra para la semana y se cruza con
    /// el inventario real (qué está disponible, dónde y a qué precio), estimando el
    /// gasto total y si cabe en el presupuesto.
    /// </summary>
    public record PlanWeeklyQuery(
        int People, decimal Budget, double Latitude, double Longitude, double RadiusKm)
        : IRequest<WeeklyPlanDto>;

    public class PlanWeeklyQueryHandler : IRequestHandler<PlanWeeklyQuery, WeeklyPlanDto>
    {
        private readonly ISearchRepository   _search;
        private readonly IClientRepository   _clients;
        private readonly ICurrentUserService _currentUser;
        private readonly IAiChatService      _ai;

        // Canasta básica cubana por defecto si la IA no está disponible.
        private static readonly string[] DefaultStaples =
        {
            "arroz", "frijoles", "aceite", "huevos", "pan", "pollo",
            "azúcar", "sal", "cebolla", "ajo", "tomate", "leche", "café", "pasta"
        };

        public PlanWeeklyQueryHandler(
            ISearchRepository search, IClientRepository clients,
            ICurrentUserService currentUser, IAiChatService ai)
        {
            _search = search; _clients = clients; _currentUser = currentUser; _ai = ai;
        }

        public async Task<WeeklyPlanDto> Handle(PlanWeeklyQuery request, CancellationToken ct)
        {
            await AiShared.RequirePremiumAsync(_clients, _currentUser, ct);

            if (request.Budget <= 0)
                throw new DomainException("Indica un presupuesto mayor que cero.");

            var people = request.People <= 0 ? 2 : request.People;
            var radius = request.RadiusKm <= 0 ? 10 : request.RadiusKm;

            var staples = await GetWeeklyStaplesAsync(people, request.Budget, ct);

            var shopping = new List<NearbyProductDto>();
            var missing  = new List<string>();
            decimal total = 0m;

            foreach (var ing in staples)
            {
                var products = await _search.SearchProductsAsync(
                    query: ing, city: null, categoryId: null, onlyAvailable: true,
                    minPrice: null, maxPrice: null, storeId: null, page: 1, pageSize: 50, cancellationToken: ct);

                var best = products
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
                    .FirstOrDefault();

                if (best is null) { missing.Add(ing); continue; }

                total += best.P.Price;
                shopping.Add(new NearbyProductDto(
                    best.P.ProductName, best.P.Price, best.P.Currency, best.P.StoreName, best.P.StoreAddress,
                    best.P.City, best.P.Latitude, best.P.Longitude,
                    best.Dist.HasValue ? Math.Round(best.Dist.Value, 2) : null));
            }

            var withinBudget = total <= request.Budget;
            var recommendation = await BuildPlanAsync(people, request.Budget, shopping, missing, total, withinBudget, ct);

            return new WeeklyPlanDto(
                recommendation, shopping, missing, Math.Round(total, 2), request.Budget, withinBudget, people);
        }

        private async Task<List<string>> GetWeeklyStaplesAsync(int people, decimal budget, CancellationToken ct)
        {
            if (!_ai.IsConfigured)
                return DefaultStaples.ToList();

            var prompt = new AiMessage("user",
                $"Arma la LISTA DE LA COMPRA semanal para una familia de {people} personas con un " +
                $"presupuesto de {budget} CUP en Cuba. Responde ÚNICAMENTE los ingredientes básicos " +
                "(máximo 15) en una sola línea separados por comas, sin cantidades ni texto extra.");

            var reply = await _ai.CompleteAsync(AiShared.FoodSystemPrompt, new[] { prompt }, ct);

            var list = reply
                .Split(new[] { ',', '\n', '·', '-' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(s => s.Length is > 1 and < 40)
                .Take(15)
                .ToList();

            return list.Count > 0 ? list : DefaultStaples.ToList();
        }

        private async Task<string> BuildPlanAsync(
            int people, decimal budget, List<NearbyProductDto> shopping, List<string> missing,
            decimal total, bool withinBudget, CancellationToken ct)
        {
            var foundLine = shopping.Count == 0 ? "nada por ahora" :
                string.Join(", ", shopping.Select(s => $"{s.Product} ({s.Price:0} {s.Currency} en {s.StoreName})"));
            var missingLine = missing.Count == 0 ? "ninguno" : string.Join(", ", missing);
            var budgetLine = withinBudget
                ? $"El total estimado ({total:0} CUP) cabe en el presupuesto de {budget:0} CUP."
                : $"El total estimado ({total:0} CUP) supera el presupuesto de {budget:0} CUP.";

            var deterministic =
                $"Plan semanal para {people} personas:\n🛒 Disponible cerca: {foundLine}\n" +
                $"❓ No encontrado: {missingLine}\n💵 {budgetLine}";

            if (!_ai.IsConfigured) return deterministic;

            var prompt = new AiMessage("user",
                $"Familia de {people} personas, presupuesto {budget} CUP. Disponible cerca (ingrediente: precio en tienda): " +
                $"{foundLine}. No encontrado: {missingLine}. Total estimado: {total} CUP ({(withinBudget ? "dentro" : "por encima")} del presupuesto). " +
                "Redacta un PLAN SEMANAL breve y útil en español: reparte comidas por días con lo disponible, " +
                "sugiere con qué sustituir lo que falta, y un par de consejos para no pasarse del presupuesto. Máximo 6 frases.");

            var narrative = await _ai.CompleteAsync(AiShared.FoodSystemPrompt, new[] { prompt }, ct);
            return string.IsNullOrWhiteSpace(narrative) ? deterministic : narrative;
        }
    }
}
