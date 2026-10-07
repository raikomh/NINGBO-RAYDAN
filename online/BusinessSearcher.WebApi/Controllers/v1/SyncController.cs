using BusinessSearcher.Application.DTOs.Sync;
using BusinessSearcher.Application.Features.TenantManagement.Commands;
using BusinessSearcher.Application.Features.TenantManagement.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using BusinessSearcher.API.Common.Authorization;
using BusinessSearcher.API.Controllers;

namespace BusinessSearcher.API.Controllers.v1
{
    /// <summary>
    /// Sincronización con instalaciones on-premise (modo Local). El dueño del negocio genera su
    /// propia API key desde acá (autoservicio, JWT normal) — nunca la pega a mano en el lado
    /// Local: la instalación Local la obtiene sola al emparejarse (ver repo local,
    /// POST /api/v1/sync/connect, con las credenciales normales de esta cuenta). Ingest/export son
    /// server-to-server, autenticados por esa API key (header X-Sync-Api-Key), no por JWT.
    /// </summary>
    [Route("api/v1/sync")]
    public class SyncController : BaseApiController
    {
        /// <summary>
        /// Genera (o reemplaza) la API key de sincronización de este negocio. El valor en claro se
        /// muestra una única vez; solo se persiste su hash. Solo Administrador.
        /// </summary>
        [HttpPost("api-key")]
        [Authorize]
        [OpsRoles] // sin roles = solo Administrador
        [ProducesResponseType(typeof(object), 200)]
        public async Task<IActionResult> GenerateApiKey(CancellationToken cancellationToken)
        {
            var result = await Mediator.Send(new GenerateTenantSyncApiKeyCommand(), cancellationToken);
            return Ok(result);
        }

        /// <summary>Revoca la API key de sincronización actual de este negocio. Solo Administrador.</summary>
        [HttpDelete("api-key")]
        [Authorize]
        [OpsRoles] // sin roles = solo Administrador
        public async Task<IActionResult> RevokeApiKey(CancellationToken cancellationToken)
        {
            await Mediator.Send(new RevokeTenantSyncApiKeyCommand(), cancellationToken);
            return NoContent();
        }

        /// <summary>
        /// Recibe un push de datos de una instalación Local. Llamada servidor a servidor
        /// autenticada por API key (no hay JWT de usuario), ver header X-Sync-Api-Key.
        /// </summary>
        [HttpPost("ingest")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(object), 200)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> Ingest(
            [FromBody] SyncPushEnvelopeDto envelope, CancellationToken cancellationToken)
        {
            var apiKey = Request.Headers["X-Sync-Api-Key"].FirstOrDefault();
            var result = await Mediator.Send(new IngestTenantSyncPushCommand(apiKey, envelope), cancellationToken);
            return Ok(result);
        }

        /// <summary>
        /// Sirve el snapshot completo del negocio dueño de la API key, para que una instalación
        /// Local lo integre (pull). Misma autenticación por API key que <see cref="Ingest"/>.
        /// </summary>
        [HttpGet("export")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(object), 200)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> Export(CancellationToken cancellationToken)
        {
            var apiKey = Request.Headers["X-Sync-Api-Key"].FirstOrDefault();
            var result = await Mediator.Send(new ExportTenantSyncDataCommand(apiKey), cancellationToken);
            return Ok(result);
        }

        /// <summary>
        /// Snapshot de suscripción del negocio dueño de la API key (aprobado/estado/próximo pago),
        /// para que una instalación Local refresque su propio estado y levante el modo solo-lectura
        /// al renovar. Misma autenticación por API key que <see cref="Ingest"/>.
        /// </summary>
        [HttpGet("status")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(object), 200)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> Status(CancellationToken cancellationToken)
        {
            var apiKey = Request.Headers["X-Sync-Api-Key"].FirstOrDefault();
            var result = await Mediator.Send(new GetTenantSyncStatusQuery(apiKey), cancellationToken);
            return Ok(result);
        }
    }
}
