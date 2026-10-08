using BusinessSearcher.Application.Commons.Interfaces;
using BusinessSearcher.Application.DTOs.Operations;
using BusinessSearcher.Domain.BoundedContext.Operations.Aggregates;
using BusinessSearcher.Domain.BoundedContext.Operations.Repositories;
using BusinessSearcher.Domain.Exceptions;
using MediatR;

namespace BusinessSearcher.Application.Features.Operations.Managers
{
    internal static class ManagerMapper
    {
        public static ManagerDto ToDto(Manager m) => new(
            m.Id, m.Code, m.Name, m.IdNumber, m.Municipality, m.Province, m.Phone, m.IsActive);
    }

    public record GetManagersQuery : IRequest<IReadOnlyList<ManagerDto>>;
    public record CreateManagerCommand(CreateManagerDto Dto) : IRequest<ManagerDto>;
    public record UpdateManagerCommand(Guid Id, UpdateManagerDto Dto) : IRequest<ManagerDto>;

    public class GetManagersHandler : IRequestHandler<GetManagersQuery, IReadOnlyList<ManagerDto>>
    {
        private readonly IManagerRepository _repo; private readonly ICurrentUserService _u;
        public GetManagersHandler(IManagerRepository repo, ICurrentUserService u) { _repo = repo; _u = u; }
        public async Task<IReadOnlyList<ManagerDto>> Handle(GetManagersQuery r, CancellationToken ct)
        {
            var t = OpsMapper.RequireTenant(_u);
            return (await _repo.GetByTenantAsync(t, ct)).Select(ManagerMapper.ToDto).ToList();
        }
    }

    public class CreateManagerHandler : IRequestHandler<CreateManagerCommand, ManagerDto>
    {
        private readonly IManagerRepository _repo; private readonly IOperationsUnitOfWork _uow; private readonly ICurrentUserService _u;
        public CreateManagerHandler(IManagerRepository repo, IOperationsUnitOfWork uow, ICurrentUserService u)
        { _repo = repo; _uow = uow; _u = u; }
        public async Task<ManagerDto> Handle(CreateManagerCommand r, CancellationToken ct)
        {
            var t = OpsMapper.RequireTenant(_u); var d = r.Dto;
            if (!string.IsNullOrWhiteSpace(d.Code) && await _repo.CodeExistsAsync(t, d.Code, null, ct))
                throw new DomainException("Ya existe un gestor con ese código.");
            var m = Manager.Create(t, d.Code, d.Name, d.IdNumber, d.Municipality, d.Province, d.Phone);
            await _repo.AddAsync(m, ct); await _uow.SaveChangesAsync(ct);
            return ManagerMapper.ToDto(m);
        }
    }

    public class UpdateManagerHandler : IRequestHandler<UpdateManagerCommand, ManagerDto>
    {
        private readonly IManagerRepository _repo; private readonly IOperationsUnitOfWork _uow; private readonly ICurrentUserService _u;
        public UpdateManagerHandler(IManagerRepository repo, IOperationsUnitOfWork uow, ICurrentUserService u)
        { _repo = repo; _uow = uow; _u = u; }
        public async Task<ManagerDto> Handle(UpdateManagerCommand r, CancellationToken ct)
        {
            var t = OpsMapper.RequireTenant(_u); var d = r.Dto;
            var m = await _repo.GetByIdAsync(t, r.Id, ct) ?? throw new DomainException("Gestor no encontrado.");
            if (!string.IsNullOrWhiteSpace(d.Code) && await _repo.CodeExistsAsync(t, d.Code, r.Id, ct))
                throw new DomainException("Ya existe un gestor con ese código.");
            m.Update(d.Code, d.Name, d.IdNumber, d.Municipality, d.Province, d.Phone, d.IsActive);
            await _repo.UpdateAsync(m, ct); await _uow.SaveChangesAsync(ct);
            return ManagerMapper.ToDto(m);
        }
    }
}
