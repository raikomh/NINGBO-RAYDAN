using BusinessSearcher.Application.Commons.Interfaces;
using BusinessSearcher.Domain.BoundedContext.Operations.Aggregates;
using BusinessSearcher.Domain.BoundedContext.Operations.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace BusinessSearcher.API.Middleware
{
    /// <summary>
    /// Auditoría del módulo de operaciones (TPV): registra cada mutación (POST/PUT/DELETE/PATCH)
    /// bajo <c>/api/v1/ops/*</c> y todo acceso denegado (401/403), con usuario, rol, IP, método,
    /// ruta y resultado. Se ejecuta después de Authentication/Authorization para poder leer
    /// <see cref="ICurrentUserService"/> ya poblado y el código de estado final de la respuesta.
    /// </summary>
    public class AuditLogMiddleware : IMiddleware
    {
        private readonly ILogger<AuditLogMiddleware> _logger;
        public AuditLogMiddleware(ILogger<AuditLogMiddleware> logger) => _logger = logger;

        private static readonly string[] MutatingMethods = { "POST", "PUT", "DELETE", "PATCH" };

        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            var path = context.Request.Path.Value ?? string.Empty;
            var isOpsPath = path.StartsWith("/api/v1/ops/", StringComparison.OrdinalIgnoreCase);
            var isMutation = MutatingMethods.Contains(context.Request.Method, StringComparer.OrdinalIgnoreCase);

            // Solo auditamos mutaciones del módulo de operaciones; las lecturas (GET) no se
            // registran para no inflar la tabla. Los accesos denegados sí, sea cual sea el método,
            // porque OpsRolesAttribute también puede bloquear un GET si en el futuro se restringe.
            if (!isOpsPath)
            {
                await next(context);
                return;
            }

            await next(context);

            var status = context.Response.StatusCode;
            var isDenied = status is 401 or 403;
            if (!isMutation && !isDenied) return;

            try
            {
                var currentUser = context.RequestServices.GetService(typeof(ICurrentUserService)) as ICurrentUserService;
                var tenantId = currentUser?.TenantId ?? Guid.Empty;
                // Sin negocio identificable no hay a quién atribuirle la entrada (ni siquiera un
                // 401 anónimo aporta valor sin TenantId); se omite en vez de escribir con Guid.Empty.
                if (tenantId == Guid.Empty) return;

                var repo = context.RequestServices.GetService(typeof(IAuditLogRepository)) as IAuditLogRepository;
                var uow = context.RequestServices.GetService(typeof(IOperationsUnitOfWork)) as IOperationsUnitOfWork;
                if (repo is null || uow is null) return;

                var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
                // .../api/v1/ops/{entity}/... → entity es el 4º segmento (índice 3).
                var entity = segments.Length > 3 ? segments[3] : null;
                var action = $"{context.Request.Method} {entity ?? path}";
                var resultStatus = isDenied ? "DENIED" : (status is >= 200 and < 300 ? "SUCCESS" : "ERROR");

                var log = AuditLog.Create(
                    tenantId, action, resultStatus,
                    userId: currentUser!.AccountId == Guid.Empty ? null : currentUser.AccountId,
                    userName: string.IsNullOrEmpty(currentUser.Email) ? null : currentUser.Email,
                    userRole: currentUser.OpsRole?.ToString(),
                    targetEntity: entity,
                    ipAddress: context.Connection.RemoteIpAddress?.ToString(),
                    method: context.Request.Method,
                    path: path);

                await repo.AddAsync(log, context.RequestAborted);
                await uow.SaveChangesAsync(context.RequestAborted);
            }
            catch (Exception ex)
            {
                // La auditoría nunca debe romper la respuesta ya emitida al cliente.
                _logger.LogWarning(ex, "No se pudo registrar la entrada de auditoría para {Method} {Path}", context.Request.Method, path);
            }
        }
    }
}
