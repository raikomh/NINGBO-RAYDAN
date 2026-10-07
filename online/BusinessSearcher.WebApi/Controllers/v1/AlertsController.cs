using BusinessSearcher.API.Controllers;
using BusinessSearcher.Application.DTOs.Radar;
using BusinessSearcher.Application.Features.Radar.Commands.CreateAlert;
using BusinessSearcher.Application.Features.Radar.Commands.DeactivateAlert;
using BusinessSearcher.Application.Features.Radar.Queries.GetMyAlerts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BusinessSearcher.API.Controllers.v1
{
    /// <summary>
    /// Alertas de radar (feature estrella, solo Premium): "avísame cuando haya pollo
    /// a menos de 3 km" / "arroz por menos de $350".
    /// </summary>
    [Route("api/v1/radar/alerts")]
    [Authorize(Roles = "Client")]
    public class AlertsController : BaseApiController
    {
        /// <summary>Crea una alerta de radar (solo clientes Premium).</summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateAlertDto dto, CancellationToken ct)
        {
            var result = await Mediator.Send(new CreateAlertCommand(
                dto.ProductName, dto.Latitude, dto.Longitude, dto.RadiusKm, dto.MaxPrice, dto.City), ct);
            return StatusCode(201, new { success = true, message = "Alerta creada.", data = result });
        }

        /// <summary>Lista las alertas del cliente autenticado.</summary>
        [HttpGet]
        public async Task<IActionResult> GetMine(CancellationToken ct)
        {
            var result = await Mediator.Send(new GetMyAlertsQuery(), ct);
            return Ok(result);
        }

        /// <summary>Desactiva una alerta propia.</summary>
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
        {
            await Mediator.Send(new DeactivateAlertCommand(id), ct);
            return Ok<object?>(null, "Alerta desactivada.");
        }
    }
}
