using BusinessSearcher.API.Controllers;
using BusinessSearcher.Application.DTOs.Radar;
using BusinessSearcher.Application.Features.Radar.Commands.FoodAssistantChat;
using BusinessSearcher.Application.Features.Radar.Queries.BudgetRecommendation;
using BusinessSearcher.Application.Features.Radar.Queries.RecipePlanner;
using BusinessSearcher.Application.Features.Radar.Queries.SmartMarket;
using BusinessSearcher.Application.Features.Radar.Queries.SupplyPrediction;
using BusinessSearcher.Application.Features.Radar.Queries.WeeklyPlan;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BusinessSearcher.API.Controllers.v1
{
    /// <summary>
    /// Asistente inteligente (Fase 4, solo Premium): chat de comida, mercado
    /// inteligente y predicción de abastecimiento. La IA (GroqCloud) se llama
    /// desde el servidor; la app nunca ve la API key.
    /// </summary>
    [Route("api/v1/radar/ai")]
    [Authorize(Roles = "Client")]
    public class AiController : BaseApiController
    {
        /// <summary>Chat con el asistente de comida. Envía el historial de la conversación.</summary>
        [HttpPost("chat")]
        public async Task<IActionResult> Chat([FromBody] AiChatRequestDto dto, CancellationToken ct)
        {
            var result = await Mediator.Send(new FoodAssistantChatCommand(dto.Messages ?? new()), ct);
            return Ok(result);
        }

        /// <summary>Mercado inteligente: resumen diario personalizado según favoritos y ubicación.</summary>
        [HttpPost("smart-market")]
        public async Task<IActionResult> SmartMarket([FromBody] SmartMarketRequestDto dto, CancellationToken ct)
        {
            var result = await Mediator.Send(new GetSmartMarketQuery(
                dto.Latitude, dto.Longitude, dto.RadiusKm, dto.Favorites ?? new(), dto.Budget), ct);
            return Ok(result);
        }

        /// <summary>Predicción heurística de abastecimiento de un producto.</summary>
        [HttpGet("predict")]
        public async Task<IActionResult> Predict(
            [FromQuery] string product, [FromQuery] string? city, CancellationToken ct)
        {
            var result = await Mediator.Send(new GetSupplyPredictionQuery(product, city), ct);
            return Ok(result);
        }

        /// <summary>
        /// Recomendación por presupuesto: el bot sugiere qué comida comprar dentro del
        /// presupuesto entre lo disponible en tiendas cercanas, y ubica cada producto.
        /// </summary>
        [HttpPost("budget-recommend")]
        public async Task<IActionResult> BudgetRecommend([FromBody] BudgetRecommendRequestDto dto, CancellationToken ct)
        {
            var result = await Mediator.Send(new GetBudgetRecommendationQuery(
                dto.Latitude, dto.Longitude, dto.RadiusKm, dto.Budget), ct);
            return Ok(result);
        }

        /// <summary>Asistente de recetas (v2): plato → ingredientes disponibles cerca + faltantes.</summary>
        [HttpPost("recipe")]
        public async Task<IActionResult> Recipe([FromBody] RecipeRequestDto dto, CancellationToken ct)
        {
            var result = await Mediator.Send(new PlanRecipeQuery(
                dto.Dish, dto.People, dto.Latitude, dto.Longitude, dto.RadiusKm, dto.Budget), ct);
            return Ok(result);
        }

        /// <summary>
        /// Planificador familiar semanal (feature 8): personas + presupuesto → lista de la
        /// compra cruzada con el inventario real (qué hay, dónde, a qué precio) y plan de comidas.
        /// </summary>
        [HttpPost("weekly-plan")]
        public async Task<IActionResult> WeeklyPlan([FromBody] WeeklyPlanRequestDto dto, CancellationToken ct)
        {
            var result = await Mediator.Send(new PlanWeeklyQuery(
                dto.People, dto.Budget, dto.Latitude, dto.Longitude, dto.RadiusKm), ct);
            return Ok(result);
        }
    }

    /// <summary>Cuerpo del request del planificador semanal.</summary>
    public record WeeklyPlanRequestDto(
        int      People,
        decimal  Budget,
        double   Latitude,
        double   Longitude,
        double   RadiusKm = 10);

    /// <summary>Cuerpo del request de mercado inteligente.</summary>
    public record SmartMarketRequestDto(
        double       Latitude,
        double       Longitude,
        double       RadiusKm,
        List<string> Favorites,
        decimal?     Budget = null);

    /// <summary>Cuerpo del request de recomendación por presupuesto.</summary>
    public record BudgetRecommendRequestDto(
        double  Latitude,
        double  Longitude,
        double  RadiusKm,
        decimal Budget);

    /// <summary>Cuerpo del request del asistente de recetas.</summary>
    public record RecipeRequestDto(
        string   Dish,
        int      People,
        double   Latitude,
        double   Longitude,
        double   RadiusKm = 10,
        decimal? Budget   = null);
}
