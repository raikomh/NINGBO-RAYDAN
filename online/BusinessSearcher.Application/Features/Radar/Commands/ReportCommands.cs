using BusinessSearcher.Application.Commons.Interfaces;
using BusinessSearcher.Application.DTOs.Radar;
using BusinessSearcher.Domain.BoundedContext.Radar.Aggregates;
using BusinessSearcher.Domain.BoundedContext.Radar.Enums;
using BusinessSearcher.Domain.BoundedContext.Radar.Repositories;
using BusinessSearcher.Domain.BoundedContext.Radar.ValueObjects;
using BusinessSearcher.Domain.Exceptions;
using FluentValidation;
using MediatR;

namespace BusinessSearcher.Application.Features.Radar
{
    // ── Mapper compartido ────────────────────────────────────────────────────────
    internal static class ReportMapper
    {
        public static ReportDto ToDto(AvailabilityReport r, DateTime now, double? distanceKm = null) => new(
            r.Id, r.ProductName, r.Status.ToString(),
            r.Location.Latitude, r.Location.Longitude,
            r.City, r.Municipality, r.PlaceName, r.StoreId,
            r.Price, r.Currency, r.PhotoUrl,
            r.CalculateConfidence(now),
            r.PositiveConfirmations, r.NegativeConfirmations,
            r.IsLikelyDepleted(now),
            r.ReporterClientId, r.ReportedAt, r.LastActivityAt, distanceKm,
            r.EstimatedMinutesAvailable(now), r.QueueStatus.ToString(), r.Source.ToString());

        public static AvailabilityStatus ParseStatus(string? value, AvailabilityStatus fallback = AvailabilityStatus.Available)
            => Enum.TryParse<AvailabilityStatus>(value, true, out var s) ? s : fallback;
    }
}

namespace BusinessSearcher.Application.Features.Radar.Commands.CreateReport
{
    using BusinessSearcher.Application.Features.Radar;

    public record CreateReportCommand(
        string   ProductName,
        string   Status,
        double   Latitude,
        double   Longitude,
        string   City,
        string   PlaceName,
        Guid?    StoreId,
        string?  Municipality,
        decimal? Price,
        string?  Currency,
        string?  PhotoUrl,
        string?  QueueStatus = null) : IRequest<ReportDto>;

    public class CreateReportCommandValidator : AbstractValidator<CreateReportCommand>
    {
        public CreateReportCommandValidator()
        {
            RuleFor(x => x.ProductName)
                .NotEmpty().WithMessage("El producto es requerido.")
                .MaximumLength(200);
            RuleFor(x => x.City).NotEmpty().WithMessage("La ciudad es requerida.").MaximumLength(120);
            RuleFor(x => x.PlaceName).NotEmpty().WithMessage("Indica el lugar donde viste el producto.").MaximumLength(200);
            RuleFor(x => x.Latitude).InclusiveBetween(-90, 90);
            RuleFor(x => x.Longitude).InclusiveBetween(-180, 180);
            RuleFor(x => x.Status)
                .Must(v => Enum.TryParse<AvailabilityStatus>(v, true, out _))
                .WithMessage("El estado debe ser Available, LowStock u OutOfStock.");
            RuleFor(x => x.Price).GreaterThanOrEqualTo(0).When(x => x.Price.HasValue);
        }
    }

    public class CreateReportCommandHandler : IRequestHandler<CreateReportCommand, ReportDto>
    {
        private readonly IAvailabilityReportRepository _reports;
        private readonly IClientRepository             _clients;
        private readonly IAvailabilityAlertRepository  _alerts;
        private readonly IRadarUnitOfWork              _uow;
        private readonly ICurrentUserService           _currentUser;
        private readonly IDateTimeService              _clock;
        private readonly IRadarNotifier                _radar;
        private readonly IFcmNotificationService       _fcm;

        public CreateReportCommandHandler(
            IAvailabilityReportRepository reports, IClientRepository clients,
            IAvailabilityAlertRepository alerts, IRadarUnitOfWork uow,
            ICurrentUserService currentUser, IDateTimeService clock,
            IRadarNotifier radar, IFcmNotificationService fcm)
        {
            _reports = reports; _clients = clients; _alerts = alerts; _uow = uow;
            _currentUser = currentUser; _clock = clock; _radar = radar; _fcm = fcm;
        }

        public async Task<ReportDto> Handle(CreateReportCommand request, CancellationToken ct)
        {
            var client = await _clients.GetByIdAsync(_currentUser.AccountId, ct)
                ?? throw new DomainException("Cliente no encontrado.");

            var report = AvailabilityReport.Create(
                client.Id,
                client.ReputationScore,
                request.ProductName,
                ReportMapper.ParseStatus(request.Status),
                new GeoLocation(request.Latitude, request.Longitude),
                request.City,
                request.PlaceName,
                request.StoreId,
                request.Municipality,
                request.Price,
                request.Currency,
                request.PhotoUrl,
                Enum.TryParse<Domain.BoundedContext.Radar.Enums.QueueStatus>(request.QueueStatus, true, out var q)
                    ? q : Domain.BoundedContext.Radar.Enums.QueueStatus.Unknown);

            client.RecordReportSubmitted();

            await _reports.AddAsync(report, ct);
            await _clients.UpdateAsync(client, ct);
            await _uow.SaveChangesAsync(ct);

            var dto = ReportMapper.ToDto(report, _clock.UtcNow);

            // Tiempo real: emite el reporte a los clientes suscritos al radar (SignalR).
            await _radar.BroadcastNewReportAsync(dto, ct);

            // Alertas Premium (feature estrella): notifica por push a quien tenga una
            // alerta activa cuyos criterios cumpla el reporte recién creado.
            await NotifyMatchingAlertsAsync(report, dto, ct);

            return dto;
        }

        private async Task NotifyMatchingAlertsAsync(AvailabilityReport report, ReportDto dto, CancellationToken ct)
        {
            var activeAlerts = await _alerts.GetActiveAsync(ct);
            // No notificamos al propio autor del reporte.
            var matched = activeAlerts
                .Where(a => a.ClientId != report.ReporterClientId && a.Matches(report))
                .ToList();
            if (matched.Count == 0) return;

            foreach (var alert in matched)
            {
                var owner = await _clients.GetByIdAsync(alert.ClientId, ct);
                if (owner?.Plan == ClientPlan.Premium && !string.IsNullOrWhiteSpace(owner.FcmToken))
                {
                    await _fcm.SendToTokenAsync(
                        owner.FcmToken!,
                        "¡Disponible cerca de ti!",
                        $"{dto.ProductName} en {dto.PlaceName} ({dto.City}).",
                        new Dictionary<string, string>
                        {
                            ["type"]     = "radar_alert",
                            ["reportId"] = dto.Id.ToString(),
                            ["alertId"]  = alert.Id.ToString()
                        },
                        ct);
                }

                alert.MarkTriggered();
                await _alerts.UpdateAsync(alert, ct);
            }

            await _uow.SaveChangesAsync(ct);
        }
    }
}

namespace BusinessSearcher.Application.Features.Radar.Commands.ConfirmReport
{
    using BusinessSearcher.Application.Features.Radar;

    public record ConfirmReportCommand(Guid ReportId, bool Agrees, string? ReportedStatus) : IRequest<ReportDto>;

    public class ConfirmReportCommandHandler : IRequestHandler<ConfirmReportCommand, ReportDto>
    {
        private readonly IAvailabilityReportRepository _reports;
        private readonly IClientRepository             _clients;
        private readonly IRadarUnitOfWork              _uow;
        private readonly ICurrentUserService           _currentUser;
        private readonly IDateTimeService              _clock;

        public ConfirmReportCommandHandler(
            IAvailabilityReportRepository reports, IClientRepository clients,
            IRadarUnitOfWork uow, ICurrentUserService currentUser, IDateTimeService clock)
        {
            _reports = reports; _clients = clients; _uow = uow; _currentUser = currentUser; _clock = clock;
        }

        public async Task<ReportDto> Handle(ConfirmReportCommand request, CancellationToken ct)
        {
            var report = await _reports.GetByIdAsync(request.ReportId, ct)
                ?? throw new DomainException("Reporte no encontrado.");

            AvailabilityStatus? reportedStatus = request.ReportedStatus is null
                ? null
                : ReportMapper.ParseStatus(request.ReportedStatus);

            var affectsReputation = report.RegisterConfirmation(_currentUser.AccountId, request.Agrees, reportedStatus);

            // Una confirmación de otro cliente ajusta la reputación del autor del reporte.
            if (affectsReputation)
            {
                var reporter = await _clients.GetByIdAsync(report.ReporterClientId, ct);
                if (reporter is not null)
                {
                    reporter.RecordConfirmationReceived(request.Agrees);
                    await _clients.UpdateAsync(reporter, ct);
                }
            }

            await _reports.UpdateAsync(report, ct);
            await _uow.SaveChangesAsync(ct);

            return ReportMapper.ToDto(report, _clock.UtcNow);
        }
    }
}

namespace BusinessSearcher.Application.Features.Radar.Commands.AttachReportPhoto
{
    using BusinessSearcher.Application.Features.Radar;

    public record AttachReportPhotoCommand(Guid ReportId, string PhotoUrl) : IRequest<ReportDto>;

    public class AttachReportPhotoCommandValidator : AbstractValidator<AttachReportPhotoCommand>
    {
        public AttachReportPhotoCommandValidator()
        {
            RuleFor(x => x.PhotoUrl).NotEmpty().WithMessage("La URL de la foto es requerida.").MaximumLength(2000);
        }
    }

    public class AttachReportPhotoCommandHandler : IRequestHandler<AttachReportPhotoCommand, ReportDto>
    {
        private readonly IAvailabilityReportRepository _reports;
        private readonly IRadarUnitOfWork              _uow;
        private readonly IDateTimeService              _clock;

        public AttachReportPhotoCommandHandler(
            IAvailabilityReportRepository reports, IRadarUnitOfWork uow, IDateTimeService clock)
        {
            _reports = reports; _uow = uow; _clock = clock;
        }

        public async Task<ReportDto> Handle(AttachReportPhotoCommand request, CancellationToken ct)
        {
            var report = await _reports.GetByIdAsync(request.ReportId, ct)
                ?? throw new DomainException("Reporte no encontrado.");

            report.AttachPhoto(request.PhotoUrl);

            await _reports.UpdateAsync(report, ct);
            await _uow.SaveChangesAsync(ct);

            return ReportMapper.ToDto(report, _clock.UtcNow);
        }
    }
}

namespace BusinessSearcher.Application.Features.Radar.Queries.GetNearbyReports
{
    using BusinessSearcher.Application.Features.Radar;
    using BusinessSearcher.Domain.BoundedContext.Radar.ValueObjects;

    public record GetNearbyReportsQuery(
        double  Latitude,
        double  Longitude,
        double  RadiusKm         = 5,
        string? ProductQuery     = null,
        string? City             = null,
        int     FreshnessMinutes = 180) : IRequest<IReadOnlyList<ReportDto>>;

    public class GetNearbyReportsQueryHandler
        : IRequestHandler<GetNearbyReportsQuery, IReadOnlyList<ReportDto>>
    {
        private readonly IAvailabilityReportRepository _reports;
        private readonly IDateTimeService              _clock;

        public GetNearbyReportsQueryHandler(IAvailabilityReportRepository reports, IDateTimeService clock)
        {
            _reports = reports; _clock = clock;
        }

        public async Task<IReadOnlyList<ReportDto>> Handle(GetNearbyReportsQuery request, CancellationToken ct)
        {
            var now    = _clock.UtcNow;
            var origin = new GeoLocation(request.Latitude, request.Longitude);
            var radius = request.RadiusKm <= 0 ? 5 : request.RadiusKm;

            var candidates = await _reports.GetRecentAsync(
                request.ProductQuery, request.City, request.FreshnessMinutes, ct);

            return candidates
                .Select(r => (Report: r, Distance: origin.DistanceKmTo(r.Location)))
                .Where(x => x.Distance <= radius)
                .OrderBy(x => x.Distance)
                .Select(x => ReportMapper.ToDto(x.Report, now, Math.Round(x.Distance, 3)))
                .ToList();
        }
    }
}

namespace BusinessSearcher.Application.Features.Radar.Queries.GetReportById
{
    using BusinessSearcher.Application.Features.Radar;

    public record GetReportByIdQuery(Guid ReportId) : IRequest<ReportDto>;

    public class GetReportByIdQueryHandler : IRequestHandler<GetReportByIdQuery, ReportDto>
    {
        private readonly IAvailabilityReportRepository _reports;
        private readonly IDateTimeService              _clock;

        public GetReportByIdQueryHandler(IAvailabilityReportRepository reports, IDateTimeService clock)
        {
            _reports = reports; _clock = clock;
        }

        public async Task<ReportDto> Handle(GetReportByIdQuery request, CancellationToken ct)
        {
            var report = await _reports.GetByIdAsync(request.ReportId, ct)
                ?? throw new DomainException("Reporte no encontrado.");
            return ReportMapper.ToDto(report, _clock.UtcNow);
        }
    }
}
