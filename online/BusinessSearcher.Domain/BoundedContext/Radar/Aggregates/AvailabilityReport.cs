using BusinessSearcher.Domain.BoundedContext.Radar.Entities;
using BusinessSearcher.Domain.BoundedContext.Radar.Enums;
using BusinessSearcher.Domain.BoundedContext.Radar.ValueObjects;
using BusinessSearcher.Domain.Common;
using BusinessSearcher.Domain.Exceptions;

namespace BusinessSearcher.Domain.BoundedContext.Radar.Aggregates
{
    /// <summary>
    /// Reporte colaborativo de disponibilidad de un producto en un lugar (feature del radar).
    /// Lo crea un Cliente; otros clientes lo confirman o desmienten. El nivel de confianza
    /// se calcula a partir de las confirmaciones, la antigüedad y la reputación del autor.
    /// </summary>
    public class AvailabilityReport : Entity, IAggregateRoot
    {
        // Ventana para considerar un producto "recién agotado" (señal "no vayas", feature 20).
        private const int DepletionWindowMinutes = 20;
        private const int DepletionConfirmationsThreshold = 3;

        public string ProductName           { get; private set; } = default!;
        /// <summary>Nombre normalizado (minúsculas, sin acentos) para búsqueda y coincidencia de alertas.</summary>
        public string NormalizedProductName { get; private set; } = default!;

        /// <summary>Tienda registrada donde se vio el producto (opcional: puede ser un lugar informal).</summary>
        public Guid?   StoreId   { get; private set; }
        /// <summary>Nombre del lugar (ej. "Mercado 23") cuando no hay una tienda registrada.</summary>
        public string  PlaceName { get; private set; } = default!;

        public GeoLocation Location     { get; private set; } = default!;
        public string      City         { get; private set; } = default!;
        public string?     Municipality { get; private set; }

        public AvailabilityStatus Status { get; private set; }

        /// <summary>Estado de la cola en el lugar (feature 8).</summary>
        public QueueStatus QueueStatus { get; private set; } = QueueStatus.Unknown;

        /// <summary>Origen del reporte: comunidad o el propio comercio (feature 17).</summary>
        public ReportSource Source { get; private set; } = ReportSource.Community;

        public decimal? Price    { get; private set; }
        public string?  Currency { get; private set; }

        /// <summary>Foto reciente del producto/lugar (feature 9).</summary>
        public string? PhotoUrl { get; private set; }

        public Guid ReporterClientId { get; private set; }
        /// <summary>Reputación del autor al momento del reporte (0–100), usada en el cálculo de confianza.</summary>
        public int  ReporterReputationSnapshot { get; private set; }

        public DateTime ReportedAt     { get; private set; }
        public DateTime LastActivityAt { get; private set; }

        private readonly List<ReportConfirmation> _confirmations = new();
        public IReadOnlyCollection<ReportConfirmation> Confirmations => _confirmations.AsReadOnly();

        private AvailabilityReport() { }

        public static AvailabilityReport Create(
            Guid reporterClientId,
            int reporterReputation,
            string productName,
            AvailabilityStatus status,
            GeoLocation location,
            string city,
            string placeName,
            Guid? storeId = null,
            string? municipality = null,
            decimal? price = null,
            string? currency = null,
            string? photoUrl = null,
            QueueStatus queueStatus = QueueStatus.Unknown,
            ReportSource source = ReportSource.Community)
        {
            if (reporterClientId == Guid.Empty)
                throw new DomainException("El reporte debe tener un autor.");
            if (string.IsNullOrWhiteSpace(productName))
                throw new DomainException("El nombre del producto es requerido.");
            if (string.IsNullOrWhiteSpace(city))
                throw new DomainException("La ciudad es requerida.");
            if (string.IsNullOrWhiteSpace(placeName) && storeId is null)
                throw new DomainException("Indica el lugar o una tienda registrada.");
            if (location is null)
                throw new DomainException("La ubicación es requerida.");
            if (price.HasValue && price.Value < 0)
                throw new DomainException("El precio no puede ser negativo.");

            var now = DateTime.UtcNow;
            var report = new AvailabilityReport
            {
                ReporterClientId           = reporterClientId,
                ReporterReputationSnapshot = Math.Clamp(reporterReputation, 0, 100),
                ProductName                = productName.Trim(),
                NormalizedProductName      = Normalize(productName),
                Status                     = status,
                Location                   = location,
                City                       = city.Trim(),
                PlaceName                  = string.IsNullOrWhiteSpace(placeName) ? string.Empty : placeName.Trim(),
                StoreId                    = storeId,
                Municipality               = string.IsNullOrWhiteSpace(municipality) ? null : municipality.Trim(),
                Price                      = price,
                Currency                   = price.HasValue ? (currency ?? "CUP") : null,
                PhotoUrl                   = photoUrl,
                QueueStatus                = queueStatus,
                Source                     = source,
                ReportedAt                 = now,
                LastActivityAt             = now
            };

            report.AddDomainEvent(new AvailabilityReportedEvent(
                report.Id, report.ReporterClientId, report.NormalizedProductName,
                report.Status, report.Location.Latitude, report.Location.Longitude, report.City));
            return report;
        }

        /// <summary>
        /// Registra (o actualiza si ya existía) la confirmación de un cliente. Devuelve true
        /// si es una confirmación nueva de un cliente distinto al autor (para actualizar reputación).
        /// </summary>
        public bool RegisterConfirmation(Guid clientId, bool agrees, AvailabilityStatus? reportedStatus = null)
        {
            if (clientId == Guid.Empty)
                throw new DomainException("La confirmación debe tener un autor.");

            var existing = _confirmations.FirstOrDefault(c => c.ClientId == clientId);
            bool isNew;
            if (existing is not null)
            {
                existing.Update(agrees, reportedStatus);
                isNew = false;
            }
            else
            {
                _confirmations.Add(new ReportConfirmation(Id, clientId, agrees, reportedStatus));
                isNew = true;
            }

            LastActivityAt = DateTime.UtcNow;
            SetUpdated();

            // La comunidad puede marcar el producto como agotado (feature 20).
            if (IsLikelyDepleted(LastActivityAt) && Status != AvailabilityStatus.OutOfStock)
            {
                Status = AvailabilityStatus.OutOfStock;
                AddDomainEvent(new ReportMarkedDepletedEvent(Id, NormalizedProductName, City));
            }

            AddDomainEvent(new ReportConfirmedEvent(Id, clientId, agrees, ReporterClientId));
            return isNew && clientId != ReporterClientId;
        }

        public int PositiveConfirmations => _confirmations.Count(c => c.Agrees);
        public int NegativeConfirmations => _confirmations.Count(c => !c.Agrees);

        /// <summary>
        /// Nivel de confianza 0–100 (feature 3). Combina reputación del autor,
        /// confirmaciones a favor/en contra y decaimiento por antigüedad. Heurística ajustable.
        /// </summary>
        public int CalculateConfidence(DateTime now)
        {
            double score = 0.40 * ReporterReputationSnapshot;      // hasta 40 pts por reputación del autor
            score += Math.Min(PositiveConfirmations * 12, 48);     // hasta 48 pts por confirmaciones
            score -= NegativeConfirmations * 18;                   // penalización por desmentidos

            var reference = _confirmations.Count > 0 ? LastActivityAt : ReportedAt;
            var minutes   = Math.Max(0, (now - reference).TotalMinutes);
            score -= minutes / 3.0;                                // ~1 pt cada 3 min de antigüedad

            return (int)Math.Round(Math.Clamp(score, 0, 100));
        }

        /// <summary>true si varios clientes confirmaron "agotado" recientemente (señal "no vayas").</summary>
        public bool IsLikelyDepleted(DateTime now)
        {
            var cutoff = now.AddMinutes(-DepletionWindowMinutes);
            var recentOutOfStock = _confirmations.Count(c =>
                c.ReportedStatus == AvailabilityStatus.OutOfStock && c.CreatedAt >= cutoff);
            return recentOutOfStock >= DepletionConfirmationsThreshold;
        }

        /// <summary>
        /// Estimación heurística (feature 5) de cuántos minutos es probable que el producto
        /// siga disponible. 0 si está agotado o la comunidad lo marca como tal. Orientativa.
        /// </summary>
        public int EstimatedMinutesAvailable(DateTime now)
        {
            if (Status == AvailabilityStatus.OutOfStock || IsLikelyDepleted(now))
                return 0;

            // Ventana base según estado: disponible ~45 min, poco stock ~15 min.
            var baseWindow = Status == AvailabilityStatus.LowStock ? 15.0 : 45.0;
            var confidence = CalculateConfidence(now);

            var reference = _confirmations.Count > 0 ? LastActivityAt : ReportedAt;
            var elapsed   = Math.Max(0, (now - reference).TotalMinutes);

            var estimate = baseWindow * (confidence / 100.0) - elapsed;
            return (int)Math.Round(Math.Clamp(estimate, 0, baseWindow));
        }

        public void AttachPhoto(string photoUrl)
        {
            if (string.IsNullOrWhiteSpace(photoUrl))
                throw new DomainException("La URL de la foto no puede estar vacía.");
            PhotoUrl = photoUrl;
            LastActivityAt = DateTime.UtcNow;
            SetUpdated();
        }

        public static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            var lowered = value.Trim().ToLowerInvariant();
            var normalized = lowered.Normalize(System.Text.NormalizationForm.FormD);
            var sb = new System.Text.StringBuilder(normalized.Length);
            foreach (var ch in normalized)
            {
                var category = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch);
                if (category != System.Globalization.UnicodeCategory.NonSpacingMark)
                    sb.Append(ch);
            }
            return sb.ToString().Normalize(System.Text.NormalizationForm.FormC);
        }
    }

    // ── Domain Events ───────────────────────────────────────────────────────────
    public record AvailabilityReportedEvent(
        Guid ReportId, Guid ReporterClientId, string NormalizedProductName,
        AvailabilityStatus Status, double Latitude, double Longitude, string City) : IDomainEvent
    {
        public DateTime OccurredOn { get; } = DateTime.UtcNow;
    }

    public record ReportConfirmedEvent(Guid ReportId, Guid ClientId, bool Agrees, Guid ReporterClientId) : IDomainEvent
    {
        public DateTime OccurredOn { get; } = DateTime.UtcNow;
    }

    public record ReportMarkedDepletedEvent(Guid ReportId, string NormalizedProductName, string City) : IDomainEvent
    {
        public DateTime OccurredOn { get; } = DateTime.UtcNow;
    }
}
