using BusinessSearcher.Application.DTOs.StoreManagement;
using BusinessSearcher.Application.Features.StoreManagement.Commands.Store;
using BusinessSearcher.Application.Features.StoreManagement.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using BusinessSearcher.API.Controllers;

namespace BusinessSearcher.API.Controllers.v1
{
    [Authorize]
    [Route("api/v1/store")]
    public class StoreController : BaseApiController
    {
        /// <summary>
        /// Configura la tienda del tenant autenticado.
        /// Si aún no tiene tienda, la crea. Si ya tiene una, la actualiza.
        /// Cada tenant tiene exactamente una tienda.
        /// </summary>
        [HttpPut]
        [ProducesResponseType(typeof(object), 200)]
        public async Task<IActionResult> Setup(
            [FromBody] SetupStoreDto dto, CancellationToken cancellationToken)
        {
            var storeId = await Mediator.Send(new SetupStoreCommand(dto), cancellationToken);
            return Ok(new { id = storeId }, "Tienda configurada exitosamente.");
        }

        /// <summary>Obtiene la tienda del tenant autenticado</summary>
        [HttpGet]
        [ProducesResponseType(typeof(object), 200)]
        public async Task<IActionResult> GetMyStore(CancellationToken cancellationToken)
        {
            var result = await Mediator.Send(new GetMyStoreQuery(), cancellationToken);
            return Ok(result);
        }

        /// <summary>Activa la tienda del tenant (vuelve a aparecer en búsquedas)</summary>
        [HttpPatch("activate")]
        [ProducesResponseType(typeof(object), 200)]
        public async Task<IActionResult> Activate(CancellationToken cancellationToken)
        {
            await Mediator.Send(new ActivateStoreCommand(), cancellationToken);
            return Ok<object?>(null, "Tienda activada exitosamente.");
        }

        /// <summary>Desactiva la tienda del tenant (deja de aparecer en búsquedas)</summary>
        [HttpPatch("deactivate")]
        [ProducesResponseType(typeof(object), 200)]
        public async Task<IActionResult> Deactivate(CancellationToken cancellationToken)
        {
            await Mediator.Send(new DeactivateStoreCommand(), cancellationToken);
            return Ok<object?>(null, "Tienda desactivada exitosamente.");
        }
    }
}
