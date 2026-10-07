using Microsoft.Extensions.Configuration;

namespace BusinessSearcher.Application.Commons
{
    // "Local" = instalación on-premise de 1 solo negocio (Docker + Postgres local,
    // sin autorregistro múltiple). "Online" (default) = SaaS multi-tenant tal cual hoy.
    public static class DeploymentMode
    {
        public const string Local = "Local";

        public static bool IsLocal(IConfiguration configuration)
            => string.Equals(configuration["Deployment:Mode"], Local, StringComparison.OrdinalIgnoreCase);
    }
}
