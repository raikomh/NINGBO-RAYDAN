using BusinessSearcher.Application.Commons.Interfaces;
using BusinessSearcher.Application.DTOs.Radar;
using BusinessSearcher.Application.Features.StoreManagement.Queries.NearbyStores;
using BusinessSearcher.Domain.BoundedContext.Radar.Aggregates;
using BusinessSearcher.Domain.BoundedContext.Radar.Enums;
using BusinessSearcher.Domain.BoundedContext.Radar.Repositories;
using MediatR;

namespace BusinessSearcher.Application.Features.Radar.Insights
{
    internal static class RadarTime
    {
        public static readonly string[] DayNames =
            { "domingo", "lunes", "martes", "miércoles", "jueves", "viernes", "sábado" };

        public static string Franja(int hour) => hour switch
        {
            >= 6 and < 12  => "mañana",
            >= 12 and < 18 => "tarde",
            >= 18 and < 24 => "noche",
            _              => "madrugada"
        };
    }
}

namespace BusinessSearcher.Application.Features.Radar.Queries.AvailabilityHistory
{
    using BusinessSearcher.Application.Features.Radar.Insights;

    /// <summary>Historial/patrones (feature 6): cuándo suele aparecer un producto por día de semana.</summary>
    public record GetAvailabilityHistoryQuery(string Product, string? City) : IRequest<AvailabilityPatternDto>;

    public class GetAvailabilityHistoryQueryHandler : IRequestHandler<GetAvailabilityHistoryQuery, AvailabilityPatternDto>
    {
        private const int Days30 = 43200;
        private readonly IAvailabilityReportRepository _reports;
        public GetAvailabilityHistoryQueryHandler(IAvailabilityReportRepository reports) => _reports = reports;

        public async Task<AvailabilityPatternDto> Handle(GetAvailabilityHistoryQuery request, CancellationToken ct)
        {
            var reports = await _reports.GetRecentAsync(request.Product, request.City, Days30, ct);

            var byDay = new List<AvailabilityByDayDto>();
            // Orden lunes..domingo
            var order = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
                                DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday };

            foreach (var d in order)
            {
                var dayReports = reports.Where(r => r.ReportedAt.ToLocalTime().DayOfWeek == d).ToList();
                if (dayReports.Count == 0) continue;

                var available = dayReports.Where(r => r.Status == AvailabilityStatus.Available).ToList();
                string? bestRange = null;
                if (available.Count > 0)
                {
                    var hour = available.GroupBy(r => r.ReportedAt.ToLocalTime().Hour)
                        .OrderByDescending(g => g.Count()).First().Key;
                    bestRange = $"{hour:00}:00–{(hour + 2) % 24:00}:00";
                }

                byDay.Add(new AvailabilityByDayDto(
                    RadarTime.DayNames[(int)d], available.Count, dayReports.Count, bestRange));
            }

            string summary;
            if (byDay.Count == 0)
                summary = $"Aún no hay suficiente historial de {request.Product} para detectar patrones.";
            else
            {
                var best = byDay.OrderByDescending(d => d.AvailableReports).First();
                summary = best.BestHourRange is not null
                    ? $"{request.Product} suele aparecer los {best.Day}, sobre todo entre {best.BestHourRange}."
                    : $"{request.Product} se ve más los {best.Day}.";
            }

            return new AvailabilityPatternDto(request.Product, byDay, summary);
        }
    }
}

namespace BusinessSearcher.Application.Features.Radar.Queries.PlaceRanking
{
    /// <summary>Ranking de lugares/tiendas (feature 7) por disponibilidad, confianza y recencia.</summary>
    public record GetPlaceRankingQuery(double? Latitude, double? Longitude, string? City) : IRequest<IReadOnlyList<PlaceRankingDto>>;

    public class GetPlaceRankingQueryHandler : IRequestHandler<GetPlaceRankingQuery, IReadOnlyList<PlaceRankingDto>>
    {
        private const int Hours72 = 4320;
        private readonly IAvailabilityReportRepository _reports;
        private readonly IDateTimeService _clock;

        public GetPlaceRankingQueryHandler(IAvailabilityReportRepository reports, IDateTimeService clock)
        {
            _reports = reports; _clock = clock;
        }

        public async Task<IReadOnlyList<PlaceRankingDto>> Handle(GetPlaceRankingQuery request, CancellationToken ct)
        {
            var now = _clock.UtcNow;
            var reports = await _reports.GetRecentAsync(null, request.City, Hours72, ct);

            return reports
                .Where(r => !string.IsNullOrWhiteSpace(r.PlaceName))
                .GroupBy(r => r.PlaceName)
                .Select(g =>
                {
                    var latest    = g.OrderByDescending(r => r.LastActivityAt).First();
                    var available = g.Count(r => r.Status == AvailabilityStatus.Available);
                    double? dist  = (request.Latitude.HasValue && request.Longitude.HasValue)
                        ? Math.Round(GeoMath.HaversineKm(request.Latitude.Value, request.Longitude.Value,
                            latest.Location.Latitude, latest.Location.Longitude), 2)
                        : null;
                    return new PlaceRankingDto(
                        g.Key, latest.City, g.Count(),
                        (int)Math.Round(100.0 * available / g.Count()),
                        (int)Math.Round(g.Average(r => r.CalculateConfidence(now))),
                        latest.LastActivityAt, dist);
                })
                .OrderByDescending(p => p.AvailabilityPercent)
                .ThenByDescending(p => p.AvgConfidence)
                .ThenByDescending(p => p.LastSeen)
                .Take(20)
                .ToList();
        }
    }
}

namespace BusinessSearcher.Application.Features.Radar.Queries.RadarTrends
{
    using BusinessSearcher.Application.Features.Radar; // ReportMapper

    /// <summary>Tendencias del día (feature 18): más reportados, recién reportados, lugares activos.</summary>
    public record GetRadarTrendsQuery : IRequest<RadarTrendsDto>;

    public class GetRadarTrendsQueryHandler : IRequestHandler<GetRadarTrendsQuery, RadarTrendsDto>
    {
        private const int Hours24 = 1440;
        private readonly IAvailabilityReportRepository _reports;
        private readonly IDateTimeService _clock;

        public GetRadarTrendsQueryHandler(IAvailabilityReportRepository reports, IDateTimeService clock)
        {
            _reports = reports; _clock = clock;
        }

        public async Task<RadarTrendsDto> Handle(GetRadarTrendsQuery request, CancellationToken ct)
        {
            var now = _clock.UtcNow;
            var reports = await _reports.GetRecentAsync(null, null, Hours24, ct);

            var top = reports
                .GroupBy(r => r.ProductName, StringComparer.OrdinalIgnoreCase)
                .Select(g => new TrendItemDto(g.Key, g.Count()))
                .OrderByDescending(t => t.Reports).Take(5).ToList();

            var recent = reports
                .OrderByDescending(r => r.ReportedAt).Take(5)
                .Select(r => ReportMapper.ToDto(r, now)).ToList();

            var places = reports
                .Where(r => !string.IsNullOrWhiteSpace(r.PlaceName))
                .GroupBy(r => r.PlaceName)
                .Select(g => new TrendPlaceDto(g.Key, g.First().City, g.Count()))
                .OrderByDescending(p => p.Reports).Take(5).ToList();

            return new RadarTrendsDto(top, recent, places, now);
        }
    }
}

namespace BusinessSearcher.Application.Features.Radar.Queries.RadarHeatmap
{
    /// <summary>Mapa de calor por municipio/ciudad (feature 13).</summary>
    public record GetRadarHeatmapQuery(string? Product) : IRequest<RadarHeatmapDto>;

    public class GetRadarHeatmapQueryHandler : IRequestHandler<GetRadarHeatmapQuery, RadarHeatmapDto>
    {
        private const int Hours72 = 4320;
        private readonly IAvailabilityReportRepository _reports;
        private readonly IDateTimeService _clock;

        public GetRadarHeatmapQueryHandler(IAvailabilityReportRepository reports, IDateTimeService clock)
        {
            _reports = reports; _clock = clock;
        }

        public async Task<RadarHeatmapDto> Handle(GetRadarHeatmapQuery request, CancellationToken ct)
        {
            var reports = await _reports.GetRecentAsync(request.Product, null, Hours72, ct);

            var cells = reports
                .GroupBy(r => string.IsNullOrWhiteSpace(r.Municipality) ? r.City : r.Municipality!)
                .Select(g => new HeatmapCellDto(
                    g.Key, g.Count(),
                    (int)Math.Round(100.0 * g.Count(r => r.Status == AvailabilityStatus.Available) / g.Count())))
                .OrderByDescending(c => c.Reports)
                .ToList();

            return new RadarHeatmapDto(request.Product, cells, _clock.UtcNow);
        }
    }
}

namespace BusinessSearcher.Application.Features.Radar.Queries.RadarRoute
{
    /// <summary>Ruta inteligente (feature 12): mejor recorrido para conseguir varios productos.</summary>
    public record GetRadarRouteQuery(double Latitude, double Longitude, List<string> Products, double RadiusKm = 10)
        : IRequest<RadarRouteDto>;

    public class GetRadarRouteQueryHandler : IRequestHandler<GetRadarRouteQuery, RadarRouteDto>
    {
        private const int Fresh180 = 180;
        private readonly IAvailabilityReportRepository _reports;

        public GetRadarRouteQueryHandler(IAvailabilityReportRepository reports) => _reports = reports;

        public async Task<RadarRouteDto> Handle(GetRadarRouteQuery request, CancellationToken ct)
        {
            var radius = request.RadiusKm <= 0 ? 10 : request.RadiusKm;
            var notFound = new List<string>();
            var candidates = new List<(string Product, AvailabilityReport Report, double Dist)>();

            foreach (var product in (request.Products ?? new()).Where(p => !string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var recent = await _reports.GetRecentAsync(product, null, Fresh180, ct);
                var best = recent
                    .Where(r => r.Status == AvailabilityStatus.Available)
                    .Select(r => (r, d: GeoMath.HaversineKm(request.Latitude, request.Longitude, r.Location.Latitude, r.Location.Longitude)))
                    .Where(x => x.d <= radius)
                    .OrderBy(x => x.d)
                    .Select(x => x.r)
                    .FirstOrDefault();

                if (best is null) notFound.Add(product);
                else candidates.Add((product, best, 0));
            }

            // Nearest-neighbor desde la ubicación del usuario.
            var stops = new List<RouteStopDto>();
            double curLat = request.Latitude, curLng = request.Longitude, total = 0;
            var remaining = candidates.ToList();

            while (remaining.Count > 0)
            {
                var next = remaining
                    .Select(c => (c, d: GeoMath.HaversineKm(curLat, curLng, c.Report.Location.Latitude, c.Report.Location.Longitude)))
                    .OrderBy(x => x.d).First();

                var leg = Math.Round(next.d, 2);
                total += leg;
                var r = next.c.Report;
                stops.Add(new RouteStopDto(next.c.Product, r.PlaceName, r.Location.Latitude, r.Location.Longitude,
                    r.Price, r.Currency, leg));

                curLat = r.Location.Latitude; curLng = r.Location.Longitude;
                remaining.Remove(next.c);
            }

            return new RadarRouteDto(stops, Math.Round(total, 2), notFound);
        }
    }
}
