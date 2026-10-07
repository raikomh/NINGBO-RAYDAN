using BusinessSearcher.Domain.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace BusinessSearcher.API.Middleware
{
    // ── Global Exception Handler ──────────────────────────────────────────────────
    public class GlobalExceptionHandler : IMiddleware
    {
        private readonly ILogger<GlobalExceptionHandler> _logger;

        public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
            => _logger = logger;

        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            try
            {
                await next(context);
            }
            catch (ValidationException ex)
            {
                await WriteJsonAsync(context, HttpStatusCode.BadRequest, new
                {
                    success = false,
                    message = "Errores de validación.",
                    errors  = ex.Errors.Select(e => new
                    {
                        field   = e.PropertyName,
                        message = e.ErrorMessage
                    })
                });
            }
            catch (ConflictException ex)
            {
                _logger.LogWarning("ConflictException: {Message}", ex.Message);
                await WriteJsonAsync(context, HttpStatusCode.Conflict, new
                {
                    success = false,
                    message = ex.Message
                });
            }
            catch (ConcurrencyConflictException ex)
            {
                _logger.LogWarning("ConcurrencyConflictException: {Message}", ex.Message);
                await WriteJsonAsync(context, HttpStatusCode.Conflict, new
                {
                    success = false,
                    message = "Otra operación modificó el mismo registro al mismo tiempo. Vuelve a intentar."
                });
            }
            catch (DomainException ex)
            {
                _logger.LogWarning("DomainException: {Message}", ex.Message);
                await WriteJsonAsync(context, HttpStatusCode.BadRequest, new
                {
                    success = false,
                    message = ex.Message
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                await WriteJsonAsync(context, HttpStatusCode.Unauthorized, new
                {
                    success = false,
                    message = ex.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error no manejado: {Message}", ex.Message);
                await WriteJsonAsync(context, HttpStatusCode.InternalServerError, new
                {
                    success = false,
                    message = "Error interno del servidor. Intenta más tarde."
                });
            }
        }

        private static Task WriteJsonAsync(HttpContext ctx, HttpStatusCode code, object body)
        {
            ctx.Response.StatusCode  = (int)code;
            ctx.Response.ContentType = "application/json";
            return ctx.Response.WriteAsync(
                JsonSerializer.Serialize(body, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                }));
        }
    }
}
