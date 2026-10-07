using BusinessSearcher.Application.Commons.Interfaces;
using BusinessSearcher.Application.DTOs.Chat;
using BusinessSearcher.Application.Features.TenantManagement.Commands;
using BusinessSearcher.Application.Features.TenantManagement.Queries;
using BusinessSearcher.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using BusinessSearcher.API.Controllers;

namespace BusinessSearcher.API.Controllers.v1
{
    /// <summary>
    /// Panel de administración del chat.
    /// Solo accesible para cuentas con rol Admin (claim "role", ver AccountRole).
    /// Tiempo real vía SignalR: conectar a /hubs/chat y llamar JoinAdminRoom().
    /// </summary>
    [Authorize]
    [Route("api/v1/admin/chat")]
    public class AdminChatController : BaseApiController
    {
        private readonly ICurrentUserService _currentUser;

        public AdminChatController(ICurrentUserService currentUser)
        {
            _currentUser = currentUser;
        }

        private IActionResult? ForbidIfNotAdmin() =>
            _currentUser.Role == AccountRole.Admin ? null : Forbid();

        /// <summary>Lista todas las conversaciones activas (una por tenant)</summary>
        [HttpGet("conversations")]
        [ProducesResponseType(typeof(object), 200)]
        [ProducesResponseType(403)]
        public async Task<IActionResult> GetAllConversations(CancellationToken cancellationToken)
        {
            var deny = ForbidIfNotAdmin();
            if (deny is not null) return deny;

            var result = await Mediator.Send(new GetAllConversationsQuery(), cancellationToken);
            return Ok(result);
        }

        /// <summary>Obtiene los mensajes de la conversación con un tenant específico</summary>
        [HttpGet("conversations/{tenantId:guid}", Name = "GetTenantConversation")]
        [ProducesResponseType(typeof(object), 200)]
        [ProducesResponseType(403)]
        public async Task<IActionResult> GetTenantConversation(
            Guid tenantId,
            [FromQuery] int page     = 1,
            [FromQuery] int pageSize = 50,
            CancellationToken cancellationToken = default)
        {
            var deny = ForbidIfNotAdmin();
            if (deny is not null) return deny;

            var result = await Mediator.Send(
                new GetTenantConversationQuery(tenantId, page, pageSize), cancellationToken);
            return Ok(result);
        }

        /// <summary>Envía una respuesta del admin al tenant indicado</summary>
        [HttpPost("conversations/{tenantId:guid}/reply")]
        [ProducesResponseType(typeof(object), 201)]
        [ProducesResponseType(403)]
        public async Task<IActionResult> Reply(
            Guid tenantId,
            [FromBody] AdminReplyDto dto,
            CancellationToken cancellationToken)
        {
            var deny = ForbidIfNotAdmin();
            if (deny is not null) return deny;

            var id = await Mediator.Send(
                new SendAdminReplyCommand(tenantId, dto.Text), cancellationToken);

            return Created("GetTenantConversation", new { tenantId }, new { id });
        }
    }
}
