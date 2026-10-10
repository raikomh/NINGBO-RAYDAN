using BusinessSearcher.Application.Commons.Interfaces;
using BusinessSearcher.Application.DTOs.Operations;
using BusinessSearcher.Domain.BoundedContext.Operations.Aggregates;
using BusinessSearcher.Domain.BoundedContext.Operations.Enums;
using BusinessSearcher.Domain.BoundedContext.Operations.Repositories;
using BusinessSearcher.Domain.Exceptions;
using MediatR;

namespace BusinessSearcher.Application.Features.Operations
{
    internal static class CashMapper
    {
        public static CashMovementDto ToDto(CashMovement m) => new(
            m.Id, m.RegisterId, m.Date, m.Type.ToString(), m.Amount, m.AmountUSD, m.Currency.ToString(), m.Description, m.UserId);

        public static CashRegisterDto ToDto(CashRegister c) => new(
            c.Id, c.WarehouseId, c.OpenDate, c.CloseDate, c.InitialAmount, c.ExpectedAmount,
            c.ActualAmount, c.Difference, c.InitialAmountUSD, c.ActualAmountUSD, c.DifferenceUSD, c.Status.ToString(),
            c.OpenedBy, c.ClosedBy, c.SalesCount, c.TotalSales, c.TotalExpenses, c.TotalCashIn, c.TotalCashOut,
            c.InventoryCountCompleted);
    }
}

// ── Caja / arqueo ──
namespace BusinessSearcher.Application.Features.Operations.CashRegisters
{
    public record OpenCashRegisterCommand(OpenCashRegisterDto Dto) : IRequest<CashRegisterDto>;
    public record CloseCashRegisterCommand(Guid Id, CloseCashRegisterDto Dto) : IRequest<CashRegisterDto>;
    public record CreateCashMovementCommand(CreateCashMovementDto Dto) : IRequest<CashMovementDto>;
    /// <summary>La caja "actual" es la de la tienda activa (<paramref name="WarehouseId"/>), no "la que
    /// tenga abierta este usuario": una misma cuenta (p.ej. Administrador) puede operar varias tiendas y
    /// cada una tiene su propia caja. Sin WarehouseId cae al comportamiento viejo (por usuario), por
    /// compatibilidad.</summary>
    public record GetCurrentCashRegisterQuery(Guid? WarehouseId = null) : IRequest<CashRegisterDto?>;
    public record GetCashRegistersQuery(DateTime? From, DateTime? To) : IRequest<IReadOnlyList<CashRegisterDto>>;
    public record GetCashMovementsQuery(Guid RegisterId) : IRequest<IReadOnlyList<CashMovementDto>>;

    public class OpenCashRegisterHandler : IRequestHandler<OpenCashRegisterCommand, CashRegisterDto>
    {
        private readonly ICashRegisterRepository _repo; private readonly IOperationsUnitOfWork _uow; private readonly ICurrentUserService _u;
        public OpenCashRegisterHandler(ICashRegisterRepository repo, IOperationsUnitOfWork uow, ICurrentUserService u) { _repo = repo; _uow = uow; _u = u; }
        public async Task<CashRegisterDto> Handle(OpenCashRegisterCommand r, CancellationToken ct)
        {
            var t = OpsMapper.RequireTenant(_u); var d = r.Dto;
            // Una caja por TIENDA, no por usuario: así una misma cuenta (p.ej. Administrador) puede tener
            // abierta la caja de Nexos y, en otra pestaña/sesión, la de HNC al mismo tiempo.
            if (d.WarehouseId is Guid warehouseId)
            {
                var existingInWarehouse = await _repo.GetOpenByWarehouseAsync(t, warehouseId, ct);
                if (existingInWarehouse.Count > 0) throw new DomainException("Esta tienda ya tiene una caja abierta. Ciérrala antes de abrir otra.");
            }
            else
            {
                var existingForUser = await _repo.GetOpenForUserAsync(t, _u.AccountId, ct);
                if (existingForUser is not null) throw new DomainException("Ya tienes una caja abierta. Ciérrala antes de abrir otra.");
            }
            var reg = CashRegister.Open(t, _u.AccountId, d.InitialAmount, d.WarehouseId, d.InitialAmountUSD);
            await _repo.AddAsync(reg, ct); await _uow.SaveChangesAsync(ct);
            return CashMapper.ToDto(reg);
        }
    }

    public class CloseCashRegisterHandler : IRequestHandler<CloseCashRegisterCommand, CashRegisterDto>
    {
        private readonly ICashRegisterRepository _repo; private readonly IOperationsUnitOfWork _uow; private readonly ICurrentUserService _u;
        public CloseCashRegisterHandler(ICashRegisterRepository repo, IOperationsUnitOfWork uow, ICurrentUserService u) { _repo = repo; _uow = uow; _u = u; }
        public async Task<CashRegisterDto> Handle(CloseCashRegisterCommand r, CancellationToken ct)
        {
            var t = OpsMapper.RequireTenant(_u); var d = r.Dto;
            var reg = await _repo.GetByIdAsync(t, r.Id, ct) ?? throw new DomainException("Caja no encontrada.");
            reg.Close(_u.AccountId, d.ActualAmount, d.ActualAmountUSD);
            await _repo.UpdateAsync(reg, ct); await _uow.SaveChangesAsync(ct);
            return CashMapper.ToDto(reg);
        }
    }

    public class CreateCashMovementHandler : IRequestHandler<CreateCashMovementCommand, CashMovementDto>
    {
        private readonly ICashRegisterRepository _registers; private readonly ICashMovementRepository _movements;
        private readonly IOperationsUnitOfWork _uow; private readonly ICurrentUserService _u;
        public CreateCashMovementHandler(ICashRegisterRepository registers, ICashMovementRepository movements, IOperationsUnitOfWork uow, ICurrentUserService u)
        { _registers = registers; _movements = movements; _uow = uow; _u = u; }
        public async Task<CashMovementDto> Handle(CreateCashMovementCommand r, CancellationToken ct)
        {
            var t = OpsMapper.RequireTenant(_u); var d = r.Dto;
            var reg = await _registers.GetByIdAsync(t, d.RegisterId, ct) ?? throw new DomainException("Caja no encontrada.");
            var type = SalesMapper.ParseEnum(d.Type, CashMovementType.In);
            var currency = SalesMapper.ParseEnum(d.Currency, Currency.CUP);
            reg.RegisterCashMovement(type, d.Amount);
            var mov = CashMovement.Create(t, reg.Id, type, d.Amount, currency, d.Description, d.AmountUSD, _u.AccountId);
            await _movements.AddAsync(mov, ct); await _registers.UpdateAsync(reg, ct); await _uow.SaveChangesAsync(ct);
            return CashMapper.ToDto(mov);
        }
    }

    public class GetCurrentCashRegisterHandler : IRequestHandler<GetCurrentCashRegisterQuery, CashRegisterDto?>
    {
        private readonly ICashRegisterRepository _repo; private readonly ICurrentUserService _u;
        public GetCurrentCashRegisterHandler(ICashRegisterRepository repo, ICurrentUserService u) { _repo = repo; _u = u; }
        public async Task<CashRegisterDto?> Handle(GetCurrentCashRegisterQuery r, CancellationToken ct)
        {
            var t = OpsMapper.RequireTenant(_u);
            CashRegister? reg;
            if (r.WarehouseId is Guid warehouseId)
                reg = (await _repo.GetOpenByWarehouseAsync(t, warehouseId, ct)).FirstOrDefault();
            else
                reg = await _repo.GetOpenForUserAsync(t, _u.AccountId, ct);
            return reg is null ? null : CashMapper.ToDto(reg);
        }
    }

    public class GetCashRegistersHandler : IRequestHandler<GetCashRegistersQuery, IReadOnlyList<CashRegisterDto>>
    {
        private readonly ICashRegisterRepository _repo; private readonly ICurrentUserService _u;
        public GetCashRegistersHandler(ICashRegisterRepository repo, ICurrentUserService u) { _repo = repo; _u = u; }
        public async Task<IReadOnlyList<CashRegisterDto>> Handle(GetCashRegistersQuery r, CancellationToken ct)
        {
            var t = OpsMapper.RequireTenant(_u);
            return (await _repo.GetByTenantAsync(t, r.From, OpsMapper.EndOfDay(r.To), ct)).Select(CashMapper.ToDto).ToList();
        }
    }

    public class GetCashMovementsHandler : IRequestHandler<GetCashMovementsQuery, IReadOnlyList<CashMovementDto>>
    {
        private readonly ICashMovementRepository _repo; private readonly ICurrentUserService _u;
        public GetCashMovementsHandler(ICashMovementRepository repo, ICurrentUserService u) { _repo = repo; _u = u; }
        public async Task<IReadOnlyList<CashMovementDto>> Handle(GetCashMovementsQuery r, CancellationToken ct)
        {
            var t = OpsMapper.RequireTenant(_u);
            return (await _repo.GetByRegisterAsync(t, r.RegisterId, ct)).Select(CashMapper.ToDto).ToList();
        }
    }
}
