using BusinessSearcher.Application.DTOs.StoreManagement;
using BusinessSearcher.Application.Features.StoreManagement.Commands.Schedule;
using BusinessSearcher.Application.Features.StoreManagement.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using BusinessSearcher.API.Controllers;

namespace BusinessSearcher.API.Controllers.v1
{
    [Authorize]
    [Route("api/v1/stores/{storeId:guid}/schedules")]
    public class SchedulesController : BaseApiController
    {
        /// <summary>Lista todos los horarios configurados de una tienda</summary>
        [HttpGet]
        [ProducesResponseType(typeof(object), 200)]
        public async Task<IActionResult> GetAll(Guid storeId, CancellationToken cancellationToken)
        {
            var result = await Mediator.Send(new GetSchedulesQuery(storeId), cancellationToken);
            return Ok(result);
        }

        /// <summary>Agrega o actualiza el horario de un día específico</summary>
        [HttpPost]
        [ProducesResponseType(typeof(object), 200)]
        public async Task<IActionResult> AddOrUpdate(
            Guid storeId, [FromBody] AddScheduleDto dto, CancellationToken cancellationToken)
        {
            var scheduleId = await Mediator.Send(new AddScheduleCommand(storeId, dto), cancellationToken);
            return Ok(new { id = scheduleId }, "Horario configurado exitosamente.");
        }
    }

    // ── Endpoint público de búsqueda (sin autenticación) ─────────────────────────
    [AllowAnonymous]
    [Route("api/v1/search")]
    public class SearchController : BaseApiController
    {
        /// <summary>
        /// Busca productos disponibles en todos los negocios.
        /// Retorna producto, tienda, dirección y disponibilidad.
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(object), 200)]
        public async Task<IActionResult> Search(
            [FromQuery] string?  q,
            [FromQuery] string?  city,
            [FromQuery] Guid?    categoryId,
            [FromQuery] bool?    available,
            [FromQuery] decimal? minPrice,
            [FromQuery] decimal? maxPrice,
            [FromQuery] Guid?    storeId  = null,
            [FromQuery] int      page     = 1,
            [FromQuery] int      pageSize = 20,
            CancellationToken    cancellationToken = default)
        {
            var query  = new Application.Features.StoreManagement.Queries.SearchProductsQuery(
                q, city, categoryId, available, minPrice, maxPrice, storeId, page, pageSize);
            var result = await Mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        /// <summary>
        /// Radar de tiendas: lista las tiendas con productos disponibles cercanas a una
        /// ubicación, ordenadas por distancia (con coordenadas para ubicarlas en el mapa).
        /// </summary>
        [HttpGet("nearby-stores")]
        [ProducesResponseType(typeof(object), 200)]
        public async Task<IActionResult> NearbyStores(
            [FromQuery] double lat,
            [FromQuery] double lng,
            [FromQuery] double radiusKm = 5,
            CancellationToken cancellationToken = default)
        {
            var result = await Mediator.Send(
                new Application.Features.StoreManagement.Queries.NearbyStores.GetNearbyStoresQuery(lat, lng, radiusKm),
                cancellationToken);
            return Ok(result);
        }
    }
}
