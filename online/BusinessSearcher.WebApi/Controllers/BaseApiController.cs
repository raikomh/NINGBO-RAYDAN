using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace BusinessSearcher.API.Controllers
{
    [ApiController]
    [Produces("application/json")]
    public abstract class BaseApiController : ControllerBase
    {
        private ISender? _mediator;
        protected ISender Mediator =>
            _mediator ??= HttpContext.RequestServices.GetRequiredService<ISender>();

        protected IActionResult Ok<T>(T data, string? message = null) =>
            base.Ok(new { success = true, message, data });

        protected IActionResult Created<T>(string routeName, object routeValues, T data) =>
            base.CreatedAtRoute(routeName, routeValues,
                new { success = true, message = "Creado exitosamente.", data });
    }
}
