using BusinessSearcher.API.Controllers;
using BusinessSearcher.Application.DTOs.Radar;
using BusinessSearcher.Application.Features.Radar.Commands.CreateClientProduct;
using BusinessSearcher.Application.Features.Radar.Commands.DeleteClientProduct;
using BusinessSearcher.Application.Features.Radar.Commands.SetClientProductAvailability;
using BusinessSearcher.Application.Features.Radar.Commands.UpdateClientProduct;
using BusinessSearcher.Application.Features.Radar.Queries.GetMyClientProducts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BusinessSearcher.API.Controllers.v1
{
    /// <summary>
    /// Productos en venta de un cliente-vendedor particular (app móvil). Aparecen en el
    /// buscador y en el mapa (pin azul). Requiere rol Client.
    /// </summary>
    [Authorize(Roles = "Client")]
    [Route("api/v1/client/products")]
    public class ClientProductsController : BaseApiController
    {
        /// <summary>Lista los productos publicados por el cliente autenticado.</summary>
        [HttpGet]
        public async Task<IActionResult> GetMine(CancellationToken ct)
        {
            var result = await Mediator.Send(new GetMyClientProductsQuery(), ct);
            return Ok(result);
        }

        /// <summary>Publica un producto (captura la ubicación GPS si se envía).</summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateClientProductDto dto, CancellationToken ct)
        {
            var result = await Mediator.Send(new CreateClientProductCommand(
                dto.Name, dto.Price, dto.Currency, dto.Description, dto.ImageUrl,
                dto.Latitude, dto.Longitude, dto.City), ct);
            return StatusCode(201, new { success = true, message = "Producto publicado.", data = result });
        }

        /// <summary>Edita un producto propio.</summary>
        [HttpPut("{productId:guid}")]
        public async Task<IActionResult> Update(Guid productId, [FromBody] UpdateClientProductDto dto, CancellationToken ct)
        {
            var result = await Mediator.Send(new UpdateClientProductCommand(
                productId, dto.Name, dto.Price, dto.Currency, dto.Description, dto.ImageUrl), ct);
            return Ok(result, "Producto actualizado.");
        }

        /// <summary>Marca un producto como disponible/agotado.</summary>
        [HttpPatch("{productId:guid}/availability")]
        public async Task<IActionResult> SetAvailability(
            Guid productId, [FromBody] SetClientProductAvailabilityDto dto, CancellationToken ct)
        {
            var result = await Mediator.Send(new SetClientProductAvailabilityCommand(productId, dto.IsAvailable), ct);
            return Ok(result, dto.IsAvailable ? "Producto marcado como disponible." : "Producto marcado como agotado.");
        }

        /// <summary>Elimina un producto propio.</summary>
        [HttpDelete("{productId:guid}")]
        public async Task<IActionResult> Delete(Guid productId, CancellationToken ct)
        {
            await Mediator.Send(new DeleteClientProductCommand(productId), ct);
            return Ok<object?>(null, "Producto eliminado.");
        }
    }
}
