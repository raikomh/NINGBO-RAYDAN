using BusinessSearcher.Domain.BoundedContext.Radar.ValueObjects;
using BusinessSearcher.Domain.Common;
using BusinessSearcher.Domain.Exceptions;

namespace BusinessSearcher.Domain.BoundedContext.Radar.Aggregates
{
    /// <summary>
    /// Alerta de radar (feature 1, la funcionalidad estrella). Un cliente premium define
    /// criterios ("pollo a menos de 3 km", "arroz por menos de $350") y recibe una
    /// notificación cuando aparece un reporte que los cumple.
    /// </summary>
    public class AvailabilityAlert : Entity, IAggregateRoot
    {
        public Guid   ClientId              { get; private set; }
        public string ProductNameFilter     { get; private set; } = default!;
        public string NormalizedProductName { get; private set; } = default!;

        /// <summary>Centro del radio de búsqueda (opcional).</summary>
        public GeoLocation? Center  { get; private set; }
        /// <summary>Radio en km desde <see cref="Center"/> (opcional).</summary>
        public double?      RadiusKm { get; private set; }

        /// <summary>Precio máximo que dispara la alerta (opcional).</summary>
        public decimal? MaxPrice { get; private set; }

        /// <summary>Restringe la alerta a una ciudad (opcional).</summary>
        public string? City { get; private set; }

        public bool      IsActive        { get; private set; } = true;
        public DateTime? LastTriggeredAt { get; private set; }

        private AvailabilityAlert() { }

        public static AvailabilityAlert Create(
            Guid clientId,
            string productName,
            GeoLocation? center = null,
            double? radiusKm = null,
            decimal? maxPrice = null,
            string? city = null)
        {
            if (clientId == Guid.Empty)
                throw new DomainException("La alerta debe pertenecer a un cliente.");
            if (string.IsNullOrWhiteSpace(productName))
                throw new DomainException("El producto de la alerta es requerido.");
            if (radiusKm is <= 0)
                throw new DomainException("El radio debe ser mayor que cero.");
            if (radiusKm.HasValue && center is null)
                throw new DomainException("Para usar un radio debes indicar una ubicación central.");
            if (maxPrice is < 0)
                throw new DomainException("El precio máximo no puede ser negativo.");

            return new AvailabilityAlert
            {
                ClientId              = clientId,
                ProductNameFilter     = productName.Trim(),
                NormalizedProductName = AvailabilityReport.Normalize(productName),
                Center                = center,
                RadiusKm              = radiusKm,
                MaxPrice              = maxPrice,
                City                  = string.IsNullOrWhiteSpace(city) ? null : city.Trim()
            };
        }

        /// <summary>Evalúa si un reporte recién creado cumple los criterios de esta alerta.</summary>
        public bool Matches(AvailabilityReport report)
        {
            if (!IsActive || report is null) return false;

            if (!report.NormalizedProductName.Contains(NormalizedProductName, StringComparison.Ordinal))
                return false;

            if (City is not null && !string.Equals(report.City, City, StringComparison.OrdinalIgnoreCase))
                return false;

            if (MaxPrice.HasValue && (!report.Price.HasValue || report.Price.Value > MaxPrice.Value))
                return false;

            if (Center is not null && RadiusKm.HasValue && !Center.IsWithinKm(report.Location, RadiusKm.Value))
                return false;

            return true;
        }

        public void MarkTriggered()
        {
            LastTriggeredAt = DateTime.UtcNow;
            SetUpdated();
        }

        public void Deactivate()
        {
            IsActive = false;
            SetUpdated();
        }

        public void Reactivate()
        {
            IsActive = true;
            SetUpdated();
        }
    }
}
