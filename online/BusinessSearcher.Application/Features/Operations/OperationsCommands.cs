using BusinessSearcher.Application.Commons.Interfaces;
using BusinessSearcher.Application.DTOs.Operations;
using BusinessSearcher.Domain.BoundedContext.Operations.Aggregates;
using BusinessSearcher.Domain.BoundedContext.Operations.Enums;
using BusinessSearcher.Domain.BoundedContext.Operations.Repositories;
using BusinessSearcher.Domain.Exceptions;
using MediatR;

namespace BusinessSearcher.Application.Features.Operations
{
    internal static class OpsMapper
    {
        public static SupplierDto ToDto(Supplier s) => new(
            s.Id, s.Name, s.Contact, s.Phone, s.Email, s.Address, s.Latitude, s.Longitude, s.Category);
        public static WarehouseDto ToDto(Warehouse w) => new(w.Id, w.Name, w.Location, w.Description);
        public static ExpenseDto ToDto(Expense e) => new(e.Id, e.Type.ToString(), e.Amount, e.AmountUSD, e.Description, e.Date);
        public static ExchangeRateDto ToDto(ExchangeRateLog r) => new(r.Id, r.Rate, r.Date);

        public static Guid RequireTenant(ICurrentUserService u)
            => u.TenantId != Guid.Empty ? u.TenantId
               : throw new DomainException("Solo un negocio autenticado puede usar el módulo de operaciones.");

        /// <summary>Un filtro "hasta esta fecha" que llega del query string trae la hora en 00:00:00
        /// (p.ej. "2026-08-23"), así que sin esto un rango de un solo día excluiría todo ese día.
        /// Se expande al último instante del día para que "hasta hoy" incluya todo hoy.</summary>
        public static DateTime? EndOfDay(DateTime? to) => to?.Date.AddDays(1).AddTicks(-1);

        /// <summary>
        /// Guarda cambios reintentando el ajuste de stock si otra operación concurrente (otra venta,
        /// compra, etc.) ya modificó el mismo renglón de <see cref="ProductStock"/> primero. Sin esto,
        /// dos ventas simultáneas del mismo producto podían pisarse el descuento de stock una a otra:
        /// ambas ventas quedaban registradas pero el inventario solo bajaba una vez (ver ProductStock,
        /// que ahora usa xmin de Postgres como token de concurrencia). Al detectar el choque, recarga el
        /// stock real desde la base y vuelve a aplicar los mismos deltas antes de reintentar guardar.
        /// </summary>
        public static async Task SaveWithStockRetryAsync(IOperationsUnitOfWork uow,
            IReadOnlyList<(Product Product, Guid WarehouseId, int Delta)> adjustments, CancellationToken ct)
        {
            const int maxAttempts = 4;
            for (var attempt = 1; ; attempt++)
            {
                try { await uow.SaveChangesAsync(ct); return; }
                catch (ConcurrencyConflictException)
                {
                    // La Infraestructura ya recargó las entidades en conflicto con el valor real de
                    // la base de datos; solo hace falta volver a aplicar los mismos deltas encima.
                    if (attempt >= maxAttempts)
                        throw new ConflictException("El stock de uno de los productos cambió por otra operación al mismo tiempo. Vuelve a intentar.");
                    foreach (var (product, warehouseId, delta) in adjustments) product.AdjustStock(warehouseId, delta);
                }
            }
        }
    }
}

// ── Proveedores ──
namespace BusinessSearcher.Application.Features.Operations.Suppliers
{
    public record CreateSupplierCommand(CreateSupplierDto Dto) : IRequest<SupplierDto>;
    public record UpdateSupplierCommand(Guid Id, CreateSupplierDto Dto) : IRequest<SupplierDto>;
    public record DeleteSupplierCommand(Guid Id) : IRequest;
    public record GetSuppliersQuery(string? Search) : IRequest<IReadOnlyList<SupplierDto>>;

    public class CreateSupplierHandler : IRequestHandler<CreateSupplierCommand, SupplierDto>
    {
        private readonly ISupplierRepository _repo; private readonly IOperationsUnitOfWork _uow; private readonly ICurrentUserService _u;
        public CreateSupplierHandler(ISupplierRepository repo, IOperationsUnitOfWork uow, ICurrentUserService u) { _repo = repo; _uow = uow; _u = u; }
        public async Task<SupplierDto> Handle(CreateSupplierCommand r, CancellationToken ct)
        {
            var t = OpsMapper.RequireTenant(_u); var d = r.Dto;
            var s = Supplier.Create(t, d.Name, d.Phone, d.Email, d.Contact, d.Address, d.Category, d.Latitude, d.Longitude);
            await _repo.AddAsync(s, ct); await _uow.SaveChangesAsync(ct);
            return OpsMapper.ToDto(s);
        }
    }

    public class UpdateSupplierHandler : IRequestHandler<UpdateSupplierCommand, SupplierDto>
    {
        private readonly ISupplierRepository _repo; private readonly IOperationsUnitOfWork _uow; private readonly ICurrentUserService _u;
        public UpdateSupplierHandler(ISupplierRepository repo, IOperationsUnitOfWork uow, ICurrentUserService u) { _repo = repo; _uow = uow; _u = u; }
        public async Task<SupplierDto> Handle(UpdateSupplierCommand r, CancellationToken ct)
        {
            var t = OpsMapper.RequireTenant(_u); var d = r.Dto;
            var s = await _repo.GetByIdAsync(t, r.Id, ct) ?? throw new DomainException("Proveedor no encontrado.");
            s.Update(d.Name, d.Phone, d.Email, d.Contact, d.Address, d.Category, d.Latitude, d.Longitude);
            await _repo.UpdateAsync(s, ct); await _uow.SaveChangesAsync(ct);
            return OpsMapper.ToDto(s);
        }
    }

    public class DeleteSupplierHandler : IRequestHandler<DeleteSupplierCommand>
    {
        private readonly ISupplierRepository _repo; private readonly IOperationsUnitOfWork _uow; private readonly ICurrentUserService _u;
        public DeleteSupplierHandler(ISupplierRepository repo, IOperationsUnitOfWork uow, ICurrentUserService u) { _repo = repo; _uow = uow; _u = u; }
        public async Task Handle(DeleteSupplierCommand r, CancellationToken ct)
        {
            var t = OpsMapper.RequireTenant(_u);
            var s = await _repo.GetByIdAsync(t, r.Id, ct) ?? throw new DomainException("Proveedor no encontrado.");
            s.Deactivate(); await _repo.UpdateAsync(s, ct); await _uow.SaveChangesAsync(ct);
        }
    }

    public class GetSuppliersHandler : IRequestHandler<GetSuppliersQuery, IReadOnlyList<SupplierDto>>
    {
        private readonly ISupplierRepository _repo; private readonly ICurrentUserService _u;
        public GetSuppliersHandler(ISupplierRepository repo, ICurrentUserService u) { _repo = repo; _u = u; }
        public async Task<IReadOnlyList<SupplierDto>> Handle(GetSuppliersQuery r, CancellationToken ct)
        {
            var t = OpsMapper.RequireTenant(_u);
            return (await _repo.GetByTenantAsync(t, r.Search, ct)).Select(OpsMapper.ToDto).ToList();
        }
    }
}

// ── Almacenes ──
namespace BusinessSearcher.Application.Features.Operations.Warehouses
{
    public record CreateWarehouseCommand(CreateWarehouseDto Dto) : IRequest<WarehouseDto>;
    public record UpdateWarehouseCommand(Guid Id, CreateWarehouseDto Dto) : IRequest<WarehouseDto>;
    public record GetWarehousesQuery : IRequest<IReadOnlyList<WarehouseDto>>;

    public class CreateWarehouseHandler : IRequestHandler<CreateWarehouseCommand, WarehouseDto>
    {
        private readonly IWarehouseRepository _repo; private readonly IOperationsUnitOfWork _uow; private readonly ICurrentUserService _u;
        public CreateWarehouseHandler(IWarehouseRepository repo, IOperationsUnitOfWork uow, ICurrentUserService u) { _repo = repo; _uow = uow; _u = u; }
        public async Task<WarehouseDto> Handle(CreateWarehouseCommand r, CancellationToken ct)
        {
            var t = OpsMapper.RequireTenant(_u); var d = r.Dto;
            var w = Warehouse.Create(t, d.Name, d.Location, d.Description);
            await _repo.AddAsync(w, ct); await _uow.SaveChangesAsync(ct);
            return OpsMapper.ToDto(w);
        }
    }

    public class UpdateWarehouseHandler : IRequestHandler<UpdateWarehouseCommand, WarehouseDto>
    {
        private readonly IWarehouseRepository _repo; private readonly IOperationsUnitOfWork _uow; private readonly ICurrentUserService _u;
        public UpdateWarehouseHandler(IWarehouseRepository repo, IOperationsUnitOfWork uow, ICurrentUserService u) { _repo = repo; _uow = uow; _u = u; }
        public async Task<WarehouseDto> Handle(UpdateWarehouseCommand r, CancellationToken ct)
        {
            var t = OpsMapper.RequireTenant(_u); var d = r.Dto;
            var w = await _repo.GetByIdAsync(t, r.Id, ct) ?? throw new DomainException("Almacén no encontrado.");
            w.Update(d.Name, d.Location, d.Description);
            await _repo.UpdateAsync(w, ct); await _uow.SaveChangesAsync(ct);
            return OpsMapper.ToDto(w);
        }
    }

    public class GetWarehousesHandler : IRequestHandler<GetWarehousesQuery, IReadOnlyList<WarehouseDto>>
    {
        private readonly IWarehouseRepository _repo; private readonly ICurrentUserService _u;
        public GetWarehousesHandler(IWarehouseRepository repo, ICurrentUserService u) { _repo = repo; _u = u; }
        public async Task<IReadOnlyList<WarehouseDto>> Handle(GetWarehousesQuery r, CancellationToken ct)
        {
            var t = OpsMapper.RequireTenant(_u);
            return (await _repo.GetByTenantAsync(t, ct)).Select(OpsMapper.ToDto).ToList();
        }
    }
}

// ── Gastos ──
namespace BusinessSearcher.Application.Features.Operations.Expenses
{
    public record CreateExpenseCommand(CreateExpenseDto Dto) : IRequest<ExpenseDto>;
    public record GetExpensesQuery(DateTime? From, DateTime? To) : IRequest<IReadOnlyList<ExpenseDto>>;

    public class CreateExpenseHandler : IRequestHandler<CreateExpenseCommand, ExpenseDto>
    {
        private readonly IExpenseRepository _repo; private readonly IOperationsUnitOfWork _uow; private readonly ICurrentUserService _u;
        public CreateExpenseHandler(IExpenseRepository repo, IOperationsUnitOfWork uow, ICurrentUserService u) { _repo = repo; _uow = uow; _u = u; }
        public async Task<ExpenseDto> Handle(CreateExpenseCommand r, CancellationToken ct)
        {
            var t = OpsMapper.RequireTenant(_u); var d = r.Dto;
            var type = Enum.TryParse<ExpenseType>(d.Type, true, out var et) ? et : ExpenseType.Other;
            var e = Expense.Create(t, type, d.Amount, d.Description, d.AmountUSD, _u.AccountId, d.RegisterId);
            await _repo.AddAsync(e, ct); await _uow.SaveChangesAsync(ct);
            return OpsMapper.ToDto(e);
        }
    }

    public class GetExpensesHandler : IRequestHandler<GetExpensesQuery, IReadOnlyList<ExpenseDto>>
    {
        private readonly IExpenseRepository _repo; private readonly ICurrentUserService _u;
        public GetExpensesHandler(IExpenseRepository repo, ICurrentUserService u) { _repo = repo; _u = u; }
        public async Task<IReadOnlyList<ExpenseDto>> Handle(GetExpensesQuery r, CancellationToken ct)
        {
            var t = OpsMapper.RequireTenant(_u);
            return (await _repo.GetByTenantAsync(t, r.From, OpsMapper.EndOfDay(r.To), ct)).Select(OpsMapper.ToDto).ToList();
        }
    }
}

// ── Tasa de cambio ──
namespace BusinessSearcher.Application.Features.Operations.ExchangeRates
{
    public record SetExchangeRateCommand(decimal Rate) : IRequest<ExchangeRateDto>;
    public record GetCurrentExchangeRateQuery : IRequest<ExchangeRateDto?>;
    public record GetExchangeRateHistoryQuery(int Limit = 30) : IRequest<IReadOnlyList<ExchangeRateDto>>;

    public class SetExchangeRateHandler : IRequestHandler<SetExchangeRateCommand, ExchangeRateDto>
    {
        private readonly IExchangeRateRepository _repo; private readonly IOperationsUnitOfWork _uow; private readonly ICurrentUserService _u;
        public SetExchangeRateHandler(IExchangeRateRepository repo, IOperationsUnitOfWork uow, ICurrentUserService u) { _repo = repo; _uow = uow; _u = u; }
        public async Task<ExchangeRateDto> Handle(SetExchangeRateCommand r, CancellationToken ct)
        {
            var t = OpsMapper.RequireTenant(_u);
            var log = ExchangeRateLog.Create(t, r.Rate);
            await _repo.AddAsync(log, ct); await _uow.SaveChangesAsync(ct);
            return OpsMapper.ToDto(log);
        }
    }

    public class GetCurrentExchangeRateHandler : IRequestHandler<GetCurrentExchangeRateQuery, ExchangeRateDto?>
    {
        private readonly IExchangeRateRepository _repo; private readonly ICurrentUserService _u;
        public GetCurrentExchangeRateHandler(IExchangeRateRepository repo, ICurrentUserService u) { _repo = repo; _u = u; }
        public async Task<ExchangeRateDto?> Handle(GetCurrentExchangeRateQuery r, CancellationToken ct)
        {
            var t = OpsMapper.RequireTenant(_u);
            var log = await _repo.GetLatestAsync(t, ct);
            return log is null ? null : OpsMapper.ToDto(log);
        }
    }

    public class GetExchangeRateHistoryHandler : IRequestHandler<GetExchangeRateHistoryQuery, IReadOnlyList<ExchangeRateDto>>
    {
        private readonly IExchangeRateRepository _repo; private readonly ICurrentUserService _u;
        public GetExchangeRateHistoryHandler(IExchangeRateRepository repo, ICurrentUserService u) { _repo = repo; _u = u; }
        public async Task<IReadOnlyList<ExchangeRateDto>> Handle(GetExchangeRateHistoryQuery r, CancellationToken ct)
        {
            var t = OpsMapper.RequireTenant(_u);
            return (await _repo.GetHistoryAsync(t, r.Limit, ct)).Select(OpsMapper.ToDto).ToList();
        }
    }
}

// ── Roles y salarios (solo Administrador puede leer/modificar/eliminar) ──
namespace BusinessSearcher.Application.Features.Operations.RoleSalaryConfigs
{
    public record GetRoleSalaryConfigsQuery : IRequest<IReadOnlyList<RoleSalaryConfigDto>>;
    public record SaveRoleSalaryConfigCommand(OperationsRole Role, SaveRoleSalaryConfigDto Dto) : IRequest<RoleSalaryConfigDto>;
    public record DeleteRoleSalaryConfigCommand(OperationsRole Role) : IRequest;

    public class GetRoleSalaryConfigsHandler : IRequestHandler<GetRoleSalaryConfigsQuery, IReadOnlyList<RoleSalaryConfigDto>>
    {
        private readonly IRoleSalaryConfigRepository _repo; private readonly ICurrentUserService _u;
        public GetRoleSalaryConfigsHandler(IRoleSalaryConfigRepository repo, ICurrentUserService u) { _repo = repo; _u = u; }
        public async Task<IReadOnlyList<RoleSalaryConfigDto>> Handle(GetRoleSalaryConfigsQuery r, CancellationToken ct)
        {
            var t = OpsMapper.RequireTenant(_u);
            var configs = await _repo.GetByTenantAsync(t, ct);
            return Enum.GetValues<OperationsRole>().Select(role =>
            {
                var c = configs.FirstOrDefault(x => x.Role == role);
                return c is null
                    ? new RoleSalaryConfigDto(role.ToString(), 0, 0, false)
                    : new RoleSalaryConfigDto(role.ToString(), c.BaseSalary, c.SalesPercentage, true);
            }).ToList();
        }
    }

    public class SaveRoleSalaryConfigHandler : IRequestHandler<SaveRoleSalaryConfigCommand, RoleSalaryConfigDto>
    {
        private readonly IRoleSalaryConfigRepository _repo; private readonly IOperationsUnitOfWork _uow; private readonly ICurrentUserService _u;
        public SaveRoleSalaryConfigHandler(IRoleSalaryConfigRepository repo, IOperationsUnitOfWork uow, ICurrentUserService u) { _repo = repo; _uow = uow; _u = u; }
        public async Task<RoleSalaryConfigDto> Handle(SaveRoleSalaryConfigCommand r, CancellationToken ct)
        {
            var t = OpsMapper.RequireTenant(_u); var d = r.Dto;
            var existing = await _repo.GetByRoleAsync(t, r.Role, ct);
            if (existing is null)
            {
                var created = RoleSalaryConfig.Create(t, r.Role, d.BaseSalary, d.SalesPercentage);
                await _repo.AddAsync(created, ct);
            }
            else
            {
                existing.Update(d.BaseSalary, d.SalesPercentage);
                await _repo.UpdateAsync(existing, ct);
            }
            await _uow.SaveChangesAsync(ct);
            return new RoleSalaryConfigDto(r.Role.ToString(), d.BaseSalary, d.SalesPercentage, true);
        }
    }

    public class DeleteRoleSalaryConfigHandler : IRequestHandler<DeleteRoleSalaryConfigCommand>
    {
        private readonly IRoleSalaryConfigRepository _repo; private readonly IOperationsUnitOfWork _uow; private readonly ICurrentUserService _u;
        public DeleteRoleSalaryConfigHandler(IRoleSalaryConfigRepository repo, IOperationsUnitOfWork uow, ICurrentUserService u) { _repo = repo; _uow = uow; _u = u; }
        public async Task Handle(DeleteRoleSalaryConfigCommand r, CancellationToken ct)
        {
            var t = OpsMapper.RequireTenant(_u);
            var existing = await _repo.GetByRoleAsync(t, r.Role, ct);
            if (existing is null) return;
            await _repo.DeleteAsync(existing, ct);
            await _uow.SaveChangesAsync(ct);
        }
    }
}
