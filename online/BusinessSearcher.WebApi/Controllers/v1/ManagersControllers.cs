using BusinessSearcher.API.Common.Authorization;
using BusinessSearcher.Application.DTOs.Operations;
using BusinessSearcher.Application.Features.Operations.Managers;
using BusinessSearcher.Domain.BoundedContext.Operations.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BusinessSearcher.API.Controllers.v1
{
    /// <summary>
    /// Gestores de ventas (TPV). Cualquier usuario del negocio puede consultarlos (el POS necesita
    /// validar el código); solo el Administrador y el Observador pueden crearlos o editarlos.
    /// </summary>
    [Authorize]
    [Route("api/v1/ops/managers")]
    public class OpsManagersController : BaseApiController
    {
        [HttpGet]
        public async Task<IActionResult> GetAll(CancellationToken ct)
            => Ok(await Mediator.Send(new GetManagersQuery(), ct));

        [HttpPost]
        [OpsRoles(OperationsRole.Observador)]
        public async Task<IActionResult> Create([FromBody] CreateManagerDto dto, CancellationToken ct)
            => StatusCode(201, new { success = true, message = "Gestor creado.", data = await Mediator.Send(new CreateManagerCommand(dto), ct) });

        [HttpPut("{id:guid}")]
        [OpsRoles(OperationsRole.Observador)]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateManagerDto dto, CancellationToken ct)
            => Ok(await Mediator.Send(new UpdateManagerCommand(id, dto), ct), "Gestor actualizado.");
    }
}
