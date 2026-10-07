using BusinessSearcher.Domain.Common;
using BusinessSearcher.Domain.Exceptions;

namespace BusinessSearcher.Domain.BoundedContext.TenantManagement.Aggregates
{
    /// <summary>
    /// Registro de una visita con origen de campaña de marketing (ej. "?src=grupo_habana"
    /// compartido en un grupo de Facebook). Permite ver qué canal trae gente de verdad.
    /// </summary>
    public class MarketingVisit : Entity, IAggregateRoot
    {
        public string Source { get; private set; } = default!;

        private MarketingVisit() { }

        public static MarketingVisit Create(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
                throw new DomainException("El origen de la visita es requerido.");
            if (source.Length > 100)
                throw new DomainException("El origen no puede exceder 100 caracteres.");

            return new MarketingVisit { Source = source.Trim().ToLowerInvariant() };
        }
    }
}
