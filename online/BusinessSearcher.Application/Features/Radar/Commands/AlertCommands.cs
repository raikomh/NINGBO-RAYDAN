using BusinessSearcher.Application.Commons.Interfaces;
using BusinessSearcher.Application.DTOs.Radar;
using BusinessSearcher.Domain.BoundedContext.Radar.Aggregates;
using BusinessSearcher.Domain.BoundedContext.Radar.Enums;
using BusinessSearcher.Domain.BoundedContext.Radar.Repositories;
using BusinessSearcher.Domain.BoundedContext.Radar.ValueObjects;
using BusinessSearcher.Domain.Exceptions;
using FluentValidation;
using MediatR;

namespace BusinessSearcher.Application.Features.Radar.Alerts
{
    internal static class AlertMapper
    {
        public static AlertDto ToDto(AvailabilityAlert a) => new(
            a.Id, a.ProductNameFilter,
            a.Center?.Latitude, a.Center?.Longitude, a.RadiusKm,
            a.MaxPrice, a.City, a.IsActive, a.LastTriggeredAt, a.CreatedAt);
    }
}

namespace BusinessSearcher.Application.Features.Radar.Commands.CreateAlert
{
    using BusinessSearcher.Application.Features.Radar.Alerts;

    public record CreateAlertCommand(
        string   ProductName,
        double?  Latitude,
        double?  Longitude,
        double?  RadiusKm,
        decimal? MaxPrice,
        string?  City) : IRequest<AlertDto>;

    public class CreateAlertCommandValidator : AbstractValidator<CreateAlertCommand>
    {
        public CreateAlertCommandValidator()
        {
            RuleFor(x => x.ProductName).NotEmpty().WithMessage("El producto de la alerta es requerido.").MaximumLength(200);
            RuleFor(x => x.RadiusKm).GreaterThan(0).When(x => x.RadiusKm.HasValue);
            RuleFor(x => x.MaxPrice).GreaterThanOrEqualTo(0).When(x => x.MaxPrice.HasValue);
            RuleFor(x => x.Latitude).InclusiveBetween(-90, 90).When(x => x.Latitude.HasValue);
            RuleFor(x => x.Longitude).InclusiveBetween(-180, 180).When(x => x.Longitude.HasValue);
            RuleFor(x => x)
                .Must(x => !x.RadiusKm.HasValue || (x.Latitude.HasValue && x.Longitude.HasValue))
                .WithMessage("Para usar un radio debes indicar latitud y longitud.");
        }
    }

    public class CreateAlertCommandHandler : IRequestHandler<CreateAlertCommand, AlertDto>
    {
        private readonly IAvailabilityAlertRepository _alerts;
        private readonly IClientRepository            _clients;
        private readonly IRadarUnitOfWork             _uow;
        private readonly ICurrentUserService          _currentUser;

        public CreateAlertCommandHandler(
            IAvailabilityAlertRepository alerts, IClientRepository clients,
            IRadarUnitOfWork uow, ICurrentUserService currentUser)
        {
            _alerts = alerts; _clients = clients; _uow = uow; _currentUser = currentUser;
        }

        public async Task<AlertDto> Handle(CreateAlertCommand request, CancellationToken ct)
        {
            var client = await _clients.GetByIdAsync(_currentUser.AccountId, ct)
                ?? throw new DomainException("Cliente no encontrado.");

            // Las alertas de radar son un beneficio Premium.
            if (client.Plan != ClientPlan.Premium)
                throw new DomainException("Las alertas de radar están disponibles solo para clientes Premium.");

            GeoLocation? center = request.Latitude.HasValue && request.Longitude.HasValue
                ? new GeoLocation(request.Latitude.Value, request.Longitude.Value)
                : null;

            var alert = AvailabilityAlert.Create(
                client.Id, request.ProductName, center, request.RadiusKm, request.MaxPrice, request.City);

            await _alerts.AddAsync(alert, ct);
            await _uow.SaveChangesAsync(ct);

            return AlertMapper.ToDto(alert);
        }
    }
}

namespace BusinessSearcher.Application.Features.Radar.Commands.DeactivateAlert
{
    public record DeactivateAlertCommand(Guid AlertId) : IRequest;

    public class DeactivateAlertCommandHandler : IRequestHandler<DeactivateAlertCommand>
    {
        private readonly IAvailabilityAlertRepository _alerts;
        private readonly IRadarUnitOfWork             _uow;
        private readonly ICurrentUserService          _currentUser;

        public DeactivateAlertCommandHandler(
            IAvailabilityAlertRepository alerts, IRadarUnitOfWork uow, ICurrentUserService currentUser)
        {
            _alerts = alerts; _uow = uow; _currentUser = currentUser;
        }

        public async Task Handle(DeactivateAlertCommand request, CancellationToken ct)
        {
            var alert = await _alerts.GetByIdAsync(request.AlertId, ct)
                ?? throw new DomainException("Alerta no encontrada.");

            if (alert.ClientId != _currentUser.AccountId)
                throw new DomainException("No puedes modificar una alerta que no es tuya.");

            alert.Deactivate();
            await _alerts.UpdateAsync(alert, ct);
            await _uow.SaveChangesAsync(ct);
        }
    }
}

namespace BusinessSearcher.Application.Features.Radar.Queries.GetMyAlerts
{
    using BusinessSearcher.Application.Features.Radar.Alerts;

    public record GetMyAlertsQuery : IRequest<IReadOnlyList<AlertDto>>;

    public class GetMyAlertsQueryHandler : IRequestHandler<GetMyAlertsQuery, IReadOnlyList<AlertDto>>
    {
        private readonly IAvailabilityAlertRepository _alerts;
        private readonly ICurrentUserService          _currentUser;

        public GetMyAlertsQueryHandler(IAvailabilityAlertRepository alerts, ICurrentUserService currentUser)
        {
            _alerts = alerts; _currentUser = currentUser;
        }

        public async Task<IReadOnlyList<AlertDto>> Handle(GetMyAlertsQuery request, CancellationToken ct)
        {
            var alerts = await _alerts.GetByClientAsync(_currentUser.AccountId, ct);
            return alerts.Select(AlertMapper.ToDto).ToList();
        }
    }
}
