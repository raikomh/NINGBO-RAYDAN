using BusinessSearcher.Application.Commons.Interfaces;
using BusinessSearcher.Application.DTOs.Operations;
using BusinessSearcher.Domain.BoundedContext.Operations.Aggregates;
using BusinessSearcher.Domain.BoundedContext.Operations.Enums;
using BusinessSearcher.Domain.BoundedContext.Operations.Repositories;
using BusinessSearcher.Domain.BoundedContext.TenantManagement.Repositories;
using BusinessSearcher.Domain.Exceptions;
using MediatR;

namespace BusinessSearcher.Application.Features.Operations.OperationsUsers
{
    internal static class OpsUserMapper
    {
        public static OperationsUserDto ToDto(OperationsUser u) => new(
            u.Id, u.Name, u.Email, u.Role.ToString(), u.AvatarUrl, u.AssignedRegisterId, u.AssignedWarehouseId, u.IsActive);

        public static OperationsRole ParseRole(string? value)
            => Enum.TryParse<OperationsRole>(value, true, out var r) ? r : OperationsRole.Cajero;
    }

    // ── Gestión (solo Administrador) ──
    public record CreateOperationsUserCommand(CreateOperationsUserDto Dto) : IRequest<OperationsUserDto>;
    public record UpdateOperationsUserCommand(Guid Id, UpdateOperationsUserDto Dto) : IRequest<OperationsUserDto>;
    public record GetOperationsUsersQuery : IRequest<IReadOnlyList<OperationsUserDto>>;

    public class CreateOperationsUserHandler : IRequestHandler<CreateOperationsUserCommand, OperationsUserDto>
    {
        private readonly IOperationsUserRepository _repo; private readonly IOperationsUnitOfWork _uow;
        private readonly IPasswordHasher _hasher; private readonly ICurrentUserService _u;
        public CreateOperationsUserHandler(IOperationsUserRepository repo, IOperationsUnitOfWork uow, IPasswordHasher hasher, ICurrentUserService u)
        { _repo = repo; _uow = uow; _hasher = hasher; _u = u; }
        public async Task<OperationsUserDto> Handle(CreateOperationsUserCommand r, CancellationToken ct)
        {
            var t = OpsMapper.RequireTenant(_u); var d = r.Dto;
            if (string.IsNullOrWhiteSpace(d.Password) || d.Password.Length < 6)
                throw new DomainException("La contraseña debe tener al menos 6 caracteres.");
            if (await _repo.EmailExistsAsync(d.Email, ct))
                throw new DomainException("Ya existe un usuario con ese email.");
            var user = OperationsUser.Create(t, d.Name, d.Email, _hasher.Hash(d.Password),
                OpsUserMapper.ParseRole(d.Role), d.AvatarUrl, d.AssignedRegisterId, d.AssignedWarehouseId);
            await _repo.AddAsync(user, ct); await _uow.SaveChangesAsync(ct);
            return OpsUserMapper.ToDto(user);
        }
    }

    public class UpdateOperationsUserHandler : IRequestHandler<UpdateOperationsUserCommand, OperationsUserDto>
    {
        private readonly IOperationsUserRepository _repo; private readonly IOperationsUnitOfWork _uow;
        private readonly IPasswordHasher _hasher; private readonly ICurrentUserService _u;
        public UpdateOperationsUserHandler(IOperationsUserRepository repo, IOperationsUnitOfWork uow, IPasswordHasher hasher, ICurrentUserService u)
        { _repo = repo; _uow = uow; _hasher = hasher; _u = u; }
        public async Task<OperationsUserDto> Handle(UpdateOperationsUserCommand r, CancellationToken ct)
        {
            var t = OpsMapper.RequireTenant(_u); var d = r.Dto;
            var user = await _repo.GetByIdAsync(t, r.Id, ct) ?? throw new DomainException("Usuario no encontrado.");
            user.Update(d.Name, OpsUserMapper.ParseRole(d.Role), d.AvatarUrl, d.AssignedRegisterId, d.AssignedWarehouseId, d.IsActive);
            if (!string.IsNullOrWhiteSpace(d.NewPassword))
            {
                if (d.NewPassword.Length < 6) throw new DomainException("La contraseña debe tener al menos 6 caracteres.");
                user.SetPassword(_hasher.Hash(d.NewPassword));
            }
            await _repo.UpdateAsync(user, ct); await _uow.SaveChangesAsync(ct);
            return OpsUserMapper.ToDto(user);
        }
    }

    public class GetOperationsUsersHandler : IRequestHandler<GetOperationsUsersQuery, IReadOnlyList<OperationsUserDto>>
    {
        private readonly IOperationsUserRepository _repo; private readonly ICurrentUserService _u;
        public GetOperationsUsersHandler(IOperationsUserRepository repo, ICurrentUserService u) { _repo = repo; _u = u; }
        public async Task<IReadOnlyList<OperationsUserDto>> Handle(GetOperationsUsersQuery r, CancellationToken ct)
        {
            var t = OpsMapper.RequireTenant(_u);
            return (await _repo.GetByTenantAsync(t, ct)).Select(OpsUserMapper.ToDto).ToList();
        }
    }

    // ── Login del sub-usuario ──
    public record OpsLoginCommand(OpsLoginDto Dto) : IRequest<OpsLoginResultDto>;

    public class OpsLoginHandler : IRequestHandler<OpsLoginCommand, OpsLoginResultDto>
    {
        private readonly IOperationsUserRepository _repo; private readonly ITenantRepository _tenants;
        private readonly IPasswordHasher _hasher; private readonly IJwtTokenService _jwt;
        public OpsLoginHandler(IOperationsUserRepository repo, ITenantRepository tenants, IPasswordHasher hasher, IJwtTokenService jwt)
        { _repo = repo; _tenants = tenants; _hasher = hasher; _jwt = jwt; }
        public async Task<OpsLoginResultDto> Handle(OpsLoginCommand r, CancellationToken ct)
        {
            var d = r.Dto;
            var user = await _repo.GetByEmailAsync(d.Email, ct);
            if (user is null || !_hasher.Verify(d.Password, user.PasswordHash))
                throw new UnauthorizedAccessException("Credenciales inválidas.");

            var tenant = await _tenants.GetByIdAsync(user.TenantId, ct);
            var businessName = tenant?.BusinessName ?? user.Name;
            var token = _jwt.GenerateOperationsUserToken(user.Id, user.TenantId, user.Email, businessName, user.Role);
            return new OpsLoginResultDto(token, OpsUserMapper.ToDto(user), businessName);
        }
    }
}
