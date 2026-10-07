using BusinessSearcher.API.Controllers;
using BusinessSearcher.Application.DTOs.Radar;
using BusinessSearcher.Application.Features.Radar.Commands.AnswerQuestion;
using BusinessSearcher.Application.Features.Radar.Commands.AskQuestion;
using BusinessSearcher.Application.Features.Radar.Queries.RecentQuestions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BusinessSearcher.API.Controllers.v1
{
    /// <summary>
    /// Comunidad (feature 15): preguntas y respuestas entre usuarios
    /// ("¿Alguien ha visto leche en el Vedado?").
    /// </summary>
    [Route("api/v1/radar/community")]
    public class CommunityController : BaseApiController
    {
        /// <summary>Preguntas recientes de la comunidad (público).</summary>
        [HttpGet("questions")]
        [AllowAnonymous]
        public async Task<IActionResult> GetQuestions([FromQuery] string? city, CancellationToken ct)
            => Ok(await Mediator.Send(new GetRecentQuestionsQuery(city), ct));

        /// <summary>Publica una pregunta (cliente autenticado).</summary>
        [HttpPost("questions")]
        [Authorize(Roles = "Client")]
        public async Task<IActionResult> Ask([FromBody] AskQuestionDto dto, CancellationToken ct)
        {
            var result = await Mediator.Send(new AskQuestionCommand(dto.Text, dto.City, dto.ProductName), ct);
            return StatusCode(201, new { success = true, message = "Pregunta publicada.", data = result });
        }

        /// <summary>Responde una pregunta (cliente autenticado).</summary>
        [HttpPost("questions/{id:guid}/answers")]
        [Authorize(Roles = "Client")]
        public async Task<IActionResult> Answer(Guid id, [FromBody] AnswerQuestionDto dto, CancellationToken ct)
        {
            var result = await Mediator.Send(new AnswerQuestionCommand(id, dto.Text), ct);
            return Ok(result, "Respuesta publicada.");
        }
    }
}
