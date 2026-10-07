using BusinessSearcher.Application.Commons.Interfaces;
using BusinessSearcher.Application.DTOs.Operations;
using BusinessSearcher.Domain.BoundedContext.Operations.Repositories;
using MediatR;
using DomainBusinessInfo = BusinessSearcher.Domain.BoundedContext.Operations.Aggregates.BusinessInfo;
using DomainAuditLog = BusinessSearcher.Domain.BoundedContext.Operations.Aggregates.AuditLog;

namespace BusinessSearcher.Application.Features.Operations
{
    internal static class AdminMapper
    {
        public static BusinessInfoDto ToDto(DomainBusinessInfo b) => new(b.Id, b.Name, b.Address, b.Phone, b.Email, b.TaxId, b.LogoUrl);

        public static AuditLogDto ToDto(DomainAuditLog a) => new(
            a.Id, a.UserId, a.UserName, a.UserRole, a.Action, a.TargetEntity, a.Details,
            a.Timestamp, a.IpAddress, a.Status, a.Method, a.Path);
    }
}

// ── Datos del negocio ──
namespace BusinessSearcher.Application.Features.Operations.BusinessInfoFeature
{
    using DomainBusinessInfo = BusinessSearcher.Domain.BoundedContext.Operations.Aggregates.BusinessInfo;

    public record SaveBusinessInfoCommand(SaveBusinessInfoDto Dto) : IRequest<BusinessInfoDto>;
    public record GetBusinessInfoQuery : IRequest<BusinessInfoDto?>;

    public class SaveBusinessInfoHandler : IRequestHandler<SaveBusinessInfoCommand, BusinessInfoDto>
    {
        private readonly IBusinessInfoRepository _repo; private readonly IOperationsUnitOfWork _uow; private readonly ICurrentUserService _u;
        public SaveBusinessInfoHandler(IBusinessInfoRepository repo, IOperationsUnitOfWork uow, ICurrentUserService u) { _repo = repo; _uow = uow; _u = u; }
        public async Task<BusinessInfoDto> Handle(SaveBusinessInfoCommand r, CancellationToken ct)
        {
            var t = OpsMapper.RequireTenant(_u); var d = r.Dto;
            var existing = await _repo.GetByTenantAsync(t, ct);
            if (existing is null)
            {
                var info = DomainBusinessInfo.Create(t, d.Name, d.Address, d.Phone, d.Email, d.TaxId, d.LogoUrl);
                await _repo.AddAsync(info, ct); await _uow.SaveChangesAsync(ct);
                return AdminMapper.ToDto(info);
            }
            existing.Update(d.Name, d.Address, d.Phone, d.Email, d.TaxId, d.LogoUrl);
            await _repo.UpdateAsync(existing, ct); await _uow.SaveChangesAsync(ct);
            return AdminMapper.ToDto(existing);
        }
    }

    public class GetBusinessInfoHandler : IRequestHandler<GetBusinessInfoQuery, BusinessInfoDto?>
    {
        private readonly IBusinessInfoRepository _repo; private readonly ICurrentUserService _u;
        public GetBusinessInfoHandler(IBusinessInfoRepository repo, ICurrentUserService u) { _repo = repo; _u = u; }
        public async Task<BusinessInfoDto?> Handle(GetBusinessInfoQuery r, CancellationToken ct)
        {
            var t = OpsMapper.RequireTenant(_u);
            var info = await _repo.GetByTenantAsync(t, ct);
            return info is null ? null : AdminMapper.ToDto(info);
        }
    }
}

// ── Auditoría ──
namespace BusinessSearcher.Application.Features.Operations.AuditLogs
{
    public record GetAuditLogsQuery(DateTime? From, DateTime? To, Guid? UserId, string? Action, int Limit = 200) : IRequest<IReadOnlyList<AuditLogDto>>;

    public class GetAuditLogsHandler : IRequestHandler<GetAuditLogsQuery, IReadOnlyList<AuditLogDto>>
    {
        private readonly IAuditLogRepository _repo; private readonly ICurrentUserService _u;
        public GetAuditLogsHandler(IAuditLogRepository repo, ICurrentUserService u) { _repo = repo; _u = u; }
        public async Task<IReadOnlyList<AuditLogDto>> Handle(GetAuditLogsQuery r, CancellationToken ct)
        {
            var t = OpsMapper.RequireTenant(_u);
            return (await _repo.GetByTenantAsync(t, r.From, OpsMapper.EndOfDay(r.To), r.UserId, r.Action, r.Limit, ct)).Select(AdminMapper.ToDto).ToList();
        }
    }
}

// ── Configuración clave-valor ──
namespace BusinessSearcher.Application.Features.Operations.Settings
{
    using DomainSetting = BusinessSearcher.Domain.BoundedContext.Operations.Aggregates.Setting;

    public record GetSettingsQuery : IRequest<IReadOnlyList<SettingDto>>;
    public record GetSettingQuery(string Key) : IRequest<SettingDto?>;
    public record SaveSettingCommand(string Key, SaveSettingDto Dto) : IRequest<SettingDto>;

    public class GetSettingsHandler : IRequestHandler<GetSettingsQuery, IReadOnlyList<SettingDto>>
    {
        private readonly ISettingRepository _repo; private readonly ICurrentUserService _u;
        public GetSettingsHandler(ISettingRepository repo, ICurrentUserService u) { _repo = repo; _u = u; }
        public async Task<IReadOnlyList<SettingDto>> Handle(GetSettingsQuery r, CancellationToken ct)
        {
            var t = OpsMapper.RequireTenant(_u);
            return (await _repo.GetByTenantAsync(t, ct)).Select(s => new SettingDto(s.Key, s.Value)).ToList();
        }
    }

    public class GetSettingHandler : IRequestHandler<GetSettingQuery, SettingDto?>
    {
        private readonly ISettingRepository _repo; private readonly ICurrentUserService _u;
        public GetSettingHandler(ISettingRepository repo, ICurrentUserService u) { _repo = repo; _u = u; }
        public async Task<SettingDto?> Handle(GetSettingQuery r, CancellationToken ct)
        {
            var t = OpsMapper.RequireTenant(_u);
            var s = await _repo.GetByKeyAsync(t, r.Key, ct);
            return s is null ? null : new SettingDto(s.Key, s.Value);
        }
    }

    public class SaveSettingHandler : IRequestHandler<SaveSettingCommand, SettingDto>
    {
        private readonly ISettingRepository _repo; private readonly IOperationsUnitOfWork _uow; private readonly ICurrentUserService _u;
        public SaveSettingHandler(ISettingRepository repo, IOperationsUnitOfWork uow, ICurrentUserService u) { _repo = repo; _uow = uow; _u = u; }
        public async Task<SettingDto> Handle(SaveSettingCommand r, CancellationToken ct)
        {
            var t = OpsMapper.RequireTenant(_u);
            var existing = await _repo.GetByKeyAsync(t, r.Key, ct);
            if (existing is null)
            {
                var setting = DomainSetting.Create(t, r.Key, r.Dto.Value);
                await _repo.AddAsync(setting, ct); await _uow.SaveChangesAsync(ct);
                return new SettingDto(setting.Key, setting.Value);
            }
            existing.UpdateValue(r.Dto.Value);
            await _repo.UpdateAsync(existing, ct); await _uow.SaveChangesAsync(ct);
            return new SettingDto(existing.Key, existing.Value);
        }
    }
}
