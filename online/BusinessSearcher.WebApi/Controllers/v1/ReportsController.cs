using BusinessSearcher.API.Controllers;
using BusinessSearcher.Application.DTOs.Radar;
using BusinessSearcher.Application.Features.Radar.Commands.AttachReportPhoto;
using BusinessSearcher.Application.Features.Radar.Commands.ConfirmReport;
using BusinessSearcher.Application.Features.Radar.Commands.CreateReport;
using BusinessSearcher.Application.Features.Radar.Queries.GetNearbyReports;
using BusinessSearcher.Application.Features.Radar.Queries.GetReportById;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BusinessSearcher.API.Controllers.v1
{
    /// <summary>
    /// Radar colaborativo de disponibilidad: reportes de productos, confirmaciones,
    /// nivel de confianza y señal "no vayas".
    /// </summary>
    [Route("api/v1/radar/reports")]
    public class ReportsController : BaseApiController
    {
        /// <summary>Crea un reporte de disponibilidad (cualquier cliente autenticado).</summary>
        [HttpPost]
        [Authorize(Roles = "Client")]
        public async Task<IActionResult> Create([FromBody] CreateReportDto dto, CancellationToken ct)
        {
            var result = await Mediator.Send(new CreateReportCommand(
                dto.ProductName, dto.Status, dto.Latitude, dto.Longitude, dto.City, dto.PlaceName,
                dto.StoreId, dto.Municipality, dto.Price, dto.Currency, dto.PhotoUrl, dto.QueueStatus), ct);
            return StatusCode(201, new { success = true, message = "Reporte publicado.", data = result });
        }

        /// <summary>Reportes cercanos (radar público). Ordena por distancia y calcula confianza.</summary>
        [HttpGet("nearby")]
        [AllowAnonymous]
        public async Task<IActionResult> Nearby(
            [FromQuery] double lat,
            [FromQuery] double lng,
            [FromQuery] double radiusKm = 5,
            [FromQuery] string? q = null,
            [FromQuery] string? city = null,
            [FromQuery] int freshnessMinutes = 180,
            CancellationToken ct = default)
        {
            var result = await Mediator.Send(
                new GetNearbyReportsQuery(lat, lng, radiusKm, q, city, freshnessMinutes), ct);
            return Ok(result);
        }

        /// <summary>Detalle de un reporte por id.</summary>
        [HttpGet("{id:guid}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        {
            var result = await Mediator.Send(new GetReportByIdQuery(id), ct);
            return Ok(result);
        }

        /// <summary>Confirma o desmiente un reporte (feature 2). Puede indicar el estado observado.</summary>
        [HttpPost("{id:guid}/confirm")]
        [Authorize(Roles = "Client")]
        public async Task<IActionResult> Confirm(Guid id, [FromBody] ConfirmReportDto dto, CancellationToken ct)
        {
            var result = await Mediator.Send(new ConfirmReportCommand(id, dto.Agrees, dto.ReportedStatus), ct);
            return Ok(result, "Confirmación registrada.");
        }

        /// <summary>Adjunta una foto reciente a un reporte (feature 9).</summary>
        [HttpPost("{id:guid}/photo")]
        [Authorize(Roles = "Client")]
        public async Task<IActionResult> AttachPhoto(Guid id, [FromBody] AttachReportPhotoDto dto, CancellationToken ct)
        {
            var result = await Mediator.Send(new AttachReportPhotoCommand(id, dto.PhotoUrl), ct);
            return Ok(result, "Foto adjuntada.");
        }
    }
}
