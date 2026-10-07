using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Net;

namespace BusinessSearcher.Infrastructure.Services
{
    /// <summary>
    /// Rate limiter en memoria por IP. Simple y efectivo para MVP.
    /// Para producción a escala, reemplazar por Redis + AspNetCoreRateLimit.
    /// </summary>
    public class RateLimitingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IMemoryCache    _cache;
        private readonly ILogger<RateLimitingMiddleware> _logger;

        // Configuración de límites por ruta
        // OJO: los límites son por IP, y en redes con NAT compartido (habitual en Cuba)
        // una IP puede agrupar a muchos usuarios legítimos — no apretar de más.
        // Límites por IP. En Cuba una IP suele agrupar muchos usuarios por NAT compartido,
        // así que se mantienen holgados (el login además valida contraseña, no es la única defensa).
        private static readonly Dictionary<string, (int Requests, TimeSpan Window)> _routeLimits = new()
        {
            ["/api/v1/auth/register"] = (30, TimeSpan.FromHours(1)),     // 30 registros/hora por IP
            ["/api/v1/auth/login"]    = (100, TimeSpan.FromMinutes(15)), // 100 intentos/15min por IP
            ["/api/v1/auth/forgot-password"] = (20, TimeSpan.FromMinutes(15)), // 20 solicitudes/15min
            ["/api/v1/client/auth/login"]    = (100, TimeSpan.FromMinutes(15)), // login de cliente (app)
            ["/api/v1/client/auth/register"] = (30, TimeSpan.FromHours(1)),
            ["/api/v1/search"]        = (120, TimeSpan.FromMinutes(1)),  // 120 búsquedas/minuto
        };

        public RateLimitingMiddleware(RequestDelegate next, IMemoryCache cache,
            ILogger<RateLimitingMiddleware> logger)
        {
            _next   = next;
            _cache  = cache;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";

            var limit = _routeLimits.FirstOrDefault(kv => path.StartsWith(kv.Key));
            if (limit.Key is null)
            {
                await _next(context);
                return;
            }

            var ip       = GetClientIp(context);
            var cacheKey = $"ratelimit:{limit.Key}:{ip}";

            // Ventana fija: el contador vive una sola ventana desde la primera petición
            // (antes se re-extendía la expiración en cada request y nunca se reseteaba a tiempo)
            var counter = _cache.GetOrCreate(cacheKey, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = limit.Value.Window;
                return new RequestCounter();
            })!;

            if (Interlocked.Increment(ref counter.Count) > limit.Value.Requests)
            {
                _logger.LogWarning("Rate limit alcanzado: IP {Ip} en {Path}", ip, path);
                context.Response.StatusCode  = (int)HttpStatusCode.TooManyRequests;
                context.Response.ContentType = "application/json";
                context.Response.Headers["Retry-After"] = limit.Value.Window.TotalSeconds.ToString();
                await context.Response.WriteAsync(
                    "{\"success\":false,\"message\":\"Demasiadas solicitudes. Intenta más tarde.\"}");
                return;
            }

            await _next(context);
        }

        private sealed class RequestCounter
        {
            public int Count;
        }

        private static string GetClientIp(HttpContext context)
        {
            var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(forwardedFor))
                return forwardedFor.Split(',')[0].Trim();

            return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        }
    }
}
