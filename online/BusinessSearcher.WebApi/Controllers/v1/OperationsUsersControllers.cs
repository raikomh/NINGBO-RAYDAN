using BusinessSearcher.API.Common.Authorization;
using BusinessSearcher.API.Controllers;
using BusinessSearcher.Application.DTOs.Operations;
using BusinessSearcher.Application.Features.Operations.OperationsUsers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BusinessSearcher.API.Controllers.v1
{
    /// <summary>Gestión de sub-usuarios del negocio (TPV). Solo el Administrador (dueño).</summary>
    [Authorize]
    [OpsRoles] // sin roles = solo Administrador
    [Route("api/v1/ops/users")]
    public class OpsUsersController : BaseApiController
    {
        [HttpGet]
        public async Task<IActionResult> GetAll(CancellationToken ct)
            => Ok(await Mediator.Send(new GetOperationsUsersQuery(), ct));

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateOperationsUserDto dto, CancellationToken ct)
            => StatusCode(201, new { success = true, message = "Usuario creado.", data = await Mediator.Send(new CreateOperationsUserCommand(dto), ct) });

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateOperationsUserDto dto, CancellationToken ct)
            => Ok(await Mediator.Send(new UpdateOperationsUserCommand(id, dto), ct), "Usuario actualizado.");
    }

    /// <summary>Login de sub-usuarios del TPV (email + contraseña propios). Anónimo.</summary>
    [AllowAnonymous]
    [Route("api/v1/ops/auth")]
    public class OpsAuthController : BaseApiController
    {
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] OpsLoginDto dto, CancellationToken ct)
            => Ok(await Mediator.Send(new OpsLoginCommand(dto), ct), "Sesión iniciada.");
    }
}
