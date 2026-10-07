using BusinessSearcher.Application.DTOs.Chat;
using BusinessSearcher.Application.Features.TenantManagement.Commands;
using BusinessSearcher.Application.Features.TenantManagement.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using BusinessSearcher.API.Controllers;

namespace BusinessSearcher.API.Controllers.v1
{
    /// <summary>
    /// Chat de soporte entre el tenant y el equipo de administración.
    /// El tenant envía mensajes y recibe respuestas del admin.
    /// Tiempo real vía SignalR: conectar a /hubs/chat y unirse con JoinConversation(tenantId).
    /// </summary>
    [Authorize]
    [Route("api/v1/chat")]
    public class ChatController : BaseApiController
    {
        /// <summary>Envía un mensaje al admin de soporte</summary>
        [HttpPost("messages")]
        [ProducesResponseType(typeof(object), 201)]
        public async Task<IActionResult> Send(
            [FromBody] SendMessageDto dto,
            CancellationToken cancellationToken)
        {
            var id = await Mediator.Send(new SendTenantMessageCommand(dto.Text), cancellationToken);
            return Created("GetMyConversation", new { }, new { id });
        }

        /// <summary>Obtiene el historial de mensajes del tenant con el admin</summary>
        [HttpGet("messages", Name = "GetMyConversation")]
        [ProducesResponseType(typeof(object), 200)]
        public async Task<IActionResult> GetConversation(
            [FromQuery] int page     = 1,
            [FromQuery] int pageSize = 50,
            CancellationToken cancellationToken = default)
        {
            var result = await Mediator.Send(
                new GetMyConversationQuery(page, pageSize), cancellationToken);
            return Ok(result);
        }

        /// <summary>Cantidad de mensajes del admin que el tenant aún no ha leído</summary>
        [HttpGet("unread")]
        [ProducesResponseType(typeof(object), 200)]
        public async Task<IActionResult> GetUnreadCount(CancellationToken cancellationToken)
        {
            var result = await Mediator.Send(new GetUnreadCountQuery(), cancellationToken);
            return Ok(result);
        }

        /// <summary>Marca todos los mensajes del admin como leídos</summary>
        [HttpPatch("messages/read")]
        [ProducesResponseType(typeof(object), 200)]
        public async Task<IActionResult> MarkRead(CancellationToken cancellationToken)
        {
            await Mediator.Send(new MarkAdminMessagesReadCommand(), cancellationToken);
            return Ok<object?>(null, "Mensajes marcados como leídos.");
        }
    }
}
