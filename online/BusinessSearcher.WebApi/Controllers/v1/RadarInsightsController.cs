using BusinessSearcher.API.Controllers;
using BusinessSearcher.Application.Features.Radar.Queries.AvailabilityHistory;
using BusinessSearcher.Application.Features.Radar.Queries.PlaceRanking;
using BusinessSearcher.Application.Features.Radar.Queries.RadarHeatmap;
using BusinessSearcher.Application.Features.Radar.Queries.RadarRoute;
using BusinessSearcher.Application.Features.Radar.Queries.RadarTrends;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BusinessSearcher.API.Controllers.v1
{
    /// <summary>
    /// Inteligencia del radar (features 6, 7, 12, 13, 18): historial/patrones, ranking
    /// de lugares, ruta óptima, mapa de calor y tendencias. Datos públicos de la comunidad.
    /// </summary>
    [Route("api/v1/radar")]
    [AllowAnonymous]
    public class RadarInsightsController : BaseApiController
    {
        /// <summary>Historial: cuándo suele aparecer un producto por día de semana (#6).</summary>
        [HttpGet("history")]
        public async Task<IActionResult> History(
            [FromQuery] string product, [FromQuery] string? city, CancellationToken ct)
            => Ok(await Mediator.Send(new GetAvailabilityHistoryQuery(product, city), ct));

        /// <summary>Ranking de lugares por disponibilidad, confianza y recencia (#7).</summary>
        [HttpGet("ranking")]
        public async Task<IActionResult> Ranking(
            [FromQuery] double? lat, [FromQuery] double? lng, [FromQuery] string? city, CancellationToken ct)
            => Ok(await Mediator.Send(new GetPlaceRankingQuery(lat, lng, city), ct));

        /// <summary>Tendencias del día: más reportados, recientes, lugares activos (#18).</summary>
        [HttpGet("trends")]
        public async Task<IActionResult> Trends(CancellationToken ct)
            => Ok(await Mediator.Send(new GetRadarTrendsQuery(), ct));

        /// <summary>Mapa de calor por municipio/ciudad (#13).</summary>
        [HttpGet("heatmap")]
        public async Task<IActionResult> Heatmap([FromQuery] string? product, CancellationToken ct)
            => Ok(await Mediator.Send(new GetRadarHeatmapQuery(product), ct));

        /// <summary>Ruta óptima para conseguir varios productos (#12).</summary>
        [HttpPost("route")]
        public async Task<IActionResult> Route([FromBody] RadarRouteRequestDto dto, CancellationToken ct)
            => Ok(await Mediator.Send(
                new GetRadarRouteQuery(dto.Latitude, dto.Longitude, dto.Products ?? new(), dto.RadiusKm), ct));
    }

    public record RadarRouteRequestDto(
        double Latitude, double Longitude, List<string> Products, double RadiusKm = 10);
}
