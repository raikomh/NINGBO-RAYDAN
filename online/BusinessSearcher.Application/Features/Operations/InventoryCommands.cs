using BusinessSearcher.Application.Commons.Interfaces;
using BusinessSearcher.Application.DTOs.Operations;
using BusinessSearcher.Domain.BoundedContext.Operations.Aggregates;
using BusinessSearcher.Domain.BoundedContext.Operations.Enums;
using BusinessSearcher.Domain.BoundedContext.Operations.Repositories;
using BusinessSearcher.Domain.Exceptions;
using MediatR;

namespace BusinessSearcher.Application.Features.Operations
{
    internal static class InventoryMapper
    {
        public static InventoryMovementDto ToDto(InventoryMovement m) => new(
            m.Id, m.ProductId, m.ProductName, m.Type.ToString(), m.Quantity,
            m.FromWarehouseId, m.ToWarehouseId, m.Reason, m.Date);

        public static InventoryCountItemDto ToDto(InventoryCountItem i) => new(
            i.ProductId, i.ProductName, i.SystemQuantity, i.CountedQuantity, i.Difference, i.Audited);

        public static InventoryCountDto ToDto(InventoryCount c) => new(
            c.Id, c.WarehouseId, c.Status.ToString(), c.Adjusted, c.Date, c.Items.Select(ToDto).ToList());
    }
}

// ── Movimientos de inventario ──
namespace BusinessSearcher.Application.Features.Operations.InventoryMovements
{
    public record CreateInventoryMovementCommand(CreateInventoryMovementDto Dto) : IRequest<InventoryMovementDto>;
    public record GetInventoryMovementsQuery(DateTime? From, DateTime? To, Guid? ProductId, Guid? WarehouseId) : IRequest<IReadOnlyList<InventoryMovementDto>>;

    public class CreateInventoryMovementHandler : IRequestHandler<CreateInventoryMovementCommand, InventoryMovementDto>
    {
        private readonly IInventoryMovementRepository _repo; private readonly IProductRepository _products;
        private readonly IOperationsUnitOfWork _uow; private readonly ICurrentUserService _u;
        public CreateInventoryMovementHandler(IInventoryMovementRepository repo, IProductRepository products, IOperationsUnitOfWork uow, ICurrentUserService u)
        { _repo = repo; _products = products; _uow = uow; _u = u; }

        public async Task<InventoryMovementDto> Handle(CreateInventoryMovementCommand r, CancellationToken ct)
        {
            var t = OpsMapper.RequireTenant(_u); var d = r.Dto;
            var type = SalesMapper.ParseEnum(d.Type, InventoryMovementType.Entrada);
            var p = await _products.GetByIdAsync(t, d.ProductId, ct) ?? throw new DomainException("Producto no encontrado.");

            var movement = InventoryMovement.Create(t, p.Id, p.Name, type, d.Quantity, d.FromWarehouseId, d.ToWarehouseId, d.Reason, _u.AccountId);

            // Aplica el efecto sobre el stock según el tipo.
            switch (type)
            {
                case InventoryMovementType.Entrada:
                    p.AdjustStock(d.ToWarehouseId!.Value, d.Quantity); break;
                case InventoryMovementType.Salida:
                case InventoryMovementType.Merma:
                    p.AdjustStock(d.FromWarehouseId!.Value, -d.Quantity); break;
                case InventoryMovementType.Traslado:
                    p.AdjustStock(d.FromWarehouseId!.Value, -d.Quantity);
                    p.AdjustStock(d.ToWarehouseId!.Value, d.Quantity); break;
            }
            await _products.UpdateAsync(p, ct);
            await _repo.AddAsync(movement, ct);
            await _uow.SaveChangesAsync(ct);
            return InventoryMapper.ToDto(movement);
        }
    }

    public class GetInventoryMovementsHandler : IRequestHandler<GetInventoryMovementsQuery, IReadOnlyList<InventoryMovementDto>>
    {
        private readonly IInventoryMovementRepository _repo; private readonly ICurrentUserService _u;
        public GetInventoryMovementsHandler(IInventoryMovementRepository repo, ICurrentUserService u) { _repo = repo; _u = u; }
        public async Task<IReadOnlyList<InventoryMovementDto>> Handle(GetInventoryMovementsQuery r, CancellationToken ct)
        {
            var t = OpsMapper.RequireTenant(_u);
            return (await _repo.GetByTenantAsync(t, r.From, OpsMapper.EndOfDay(r.To), r.ProductId, r.WarehouseId, ct)).Select(InventoryMapper.ToDto).ToList();
        }
    }

    // ── Conversión/desglose de inventario ──
    // Consume N productos origen (a su costo actual) y produce un producto destino (existente o
    // nuevo) con otra unidad, trasladando el costo total consumido al costo promedio del destino.
    public record ConvertInventoryCommand(ConvertInventoryDto Dto) : IRequest<IReadOnlyList<InventoryMovementDto>>;

    public class ConvertInventoryHandler : IRequestHandler<ConvertInventoryCommand, IReadOnlyList<InventoryMovementDto>>
    {
        private readonly IInventoryMovementRepository _movements; private readonly IProductRepository _products;
        private readonly IOperationsUnitOfWork _uow; private readonly ICurrentUserService _u;
        public ConvertInventoryHandler(IInventoryMovementRepository movements, IProductRepository products, IOperationsUnitOfWork uow, ICurrentUserService u)
        { _movements = movements; _products = products; _uow = uow; _u = u; }

        public async Task<IReadOnlyList<InventoryMovementDto>> Handle(ConvertInventoryCommand r, CancellationToken ct)
        {
            var t = OpsMapper.RequireTenant(_u); var d = r.Dto;
            if (d.SourceItems is null || d.SourceItems.Count == 0)
                throw new DomainException("La conversión requiere al menos un producto origen.");
            if (d.DestinationQuantity <= 0)
                throw new DomainException("La cantidad del producto destino debe ser mayor que cero.");

            decimal totalSourceCost = 0;
            var movements = new List<InventoryMovement>();

            foreach (var item in d.SourceItems)
            {
                var sp = await _products.GetByIdAsync(t, item.ProductId, ct) ?? throw new DomainException($"Producto {item.ProductId} no encontrado.");
                totalSourceCost += sp.CostPrice * item.Quantity;
                sp.AdjustStock(d.WarehouseId, -item.Quantity);
                await _products.UpdateAsync(sp, ct);

                var mv = InventoryMovement.Create(t, sp.Id, sp.Name, InventoryMovementType.Ajuste, item.Quantity,
                    d.WarehouseId, null, d.Reason ?? "Conversión de inventario (consumo)", _u.AccountId);
                await _movements.AddAsync(mv, ct);
                movements.Add(mv);
            }

            var dest = d.DestinationProductId.HasValue
                ? await _products.GetByIdAsync(t, d.DestinationProductId.Value, ct) ?? throw new DomainException("Producto destino no encontrado.")
                : null;

            if (dest is null)
            {
                if (d.NewDestinationProduct is null)
                    throw new DomainException("Falta el producto destino o los datos para darlo de alta.");
                var np = d.NewDestinationProduct;
                dest = Product.Create(t, np.Name, 0, 0, np.Barcode, unit: np.Unit, categoryId: np.CategoryId,
                    sellPriceUsd: np.SellPriceUSD, minStock: np.MinStock, taxRate: np.TaxRate);
                await _products.AddAsync(dest, ct);
            }

            // Costo promedio ponderado del destino, combinando su stock existente con lo producido.
            var unitCost = totalSourceCost / d.DestinationQuantity;
            var oldDestStock = dest.TotalStock;
            var newDestCost = oldDestStock > 0
                ? Math.Round(((oldDestStock * dest.CostPrice) + (d.DestinationQuantity * unitCost)) / (oldDestStock + d.DestinationQuantity), 4)
                : Math.Round(unitCost, 4);

            dest.AdjustStock(d.WarehouseId, d.DestinationQuantity);
            dest.Update(dest.Name, newDestCost, dest.SellPrice, dest.Barcode, dest.Description, dest.Unit, dest.CategoryId,
                dest.CostPriceUSD, dest.SellPriceUSD, dest.MinStock, dest.TaxRate, dest.BatchNumber, dest.ExpirationDate, dest.ForSale);
            await _products.UpdateAsync(dest, ct);

            var destMv = InventoryMovement.Create(t, dest.Id, dest.Name, InventoryMovementType.Ajuste, d.DestinationQuantity,
                null, d.WarehouseId, d.Reason ?? "Conversión de inventario (producción)", _u.AccountId);
            await _movements.AddAsync(destMv, ct);
            movements.Add(destMv);

            await _uow.SaveChangesAsync(ct);
            return movements.Select(InventoryMapper.ToDto).ToList();
        }
    }
}

// ── Conteo de inventario ──
namespace BusinessSearcher.Application.Features.Operations.InventoryCounts
{
    public record CreateInventoryCountCommand(CreateInventoryCountDto Dto) : IRequest<InventoryCountDto>;
    public record CloseInventoryCountCommand(Guid Id, bool ApplyAdjustments) : IRequest<InventoryCountDto>;
    public record SetInventoryCountItemAuditedCommand(Guid Id, Guid ProductId, bool Audited) : IRequest<InventoryCountDto>;
    public record GetInventoryCountsQuery(Guid? WarehouseId) : IRequest<IReadOnlyList<InventoryCountDto>>;
    public record GetInventoryCountByIdQuery(Guid Id) : IRequest<InventoryCountDto>;

    public class CreateInventoryCountHandler : IRequestHandler<CreateInventoryCountCommand, InventoryCountDto>
    {
        private readonly IInventoryCountRepository _repo; private readonly IProductRepository _products;
        private readonly IOperationsUnitOfWork _uow; private readonly ICurrentUserService _u;
        public CreateInventoryCountHandler(IInventoryCountRepository repo, IProductRepository products, IOperationsUnitOfWork uow, ICurrentUserService u)
        { _repo = repo; _products = products; _uow = uow; _u = u; }

        public async Task<InventoryCountDto> Handle(CreateInventoryCountCommand r, CancellationToken ct)
        {
            var t = OpsMapper.RequireTenant(_u); var d = r.Dto;
            if (d.Items is null || d.Items.Count == 0) throw new DomainException("El conteo debe incluir al menos un producto.");

            var items = new List<InventoryCountItem>();
            foreach (var it in d.Items)
            {
                var p = await _products.GetByIdAsync(t, it.ProductId, ct) ?? throw new DomainException($"Producto {it.ProductId} no encontrado.");
                var system = p.StockFor(d.WarehouseId).Quantity;
                items.Add(InventoryCountItem.Create(p.Id, p.Name, system, it.CountedQuantity));
            }
            var count = InventoryCount.Create(t, d.WarehouseId, items, _u.AccountId);
            await _repo.AddAsync(count, ct); await _uow.SaveChangesAsync(ct);
            return InventoryMapper.ToDto(count);
        }
    }

    public class CloseInventoryCountHandler : IRequestHandler<CloseInventoryCountCommand, InventoryCountDto>
    {
        private readonly IInventoryCountRepository _repo; private readonly IProductRepository _products;
        private readonly ICashRegisterRepository _registers;
        private readonly IOperationsUnitOfWork _uow; private readonly ICurrentUserService _u;
        public CloseInventoryCountHandler(IInventoryCountRepository repo, IProductRepository products,
            ICashRegisterRepository registers, IOperationsUnitOfWork uow, ICurrentUserService u)
        { _repo = repo; _products = products; _registers = registers; _uow = uow; _u = u; }

        public async Task<InventoryCountDto> Handle(CloseInventoryCountCommand r, CancellationToken ct)
        {
            var t = OpsMapper.RequireTenant(_u);
            var count = await _repo.GetByIdAsync(t, r.Id, ct) ?? throw new DomainException("Conteo no encontrado.");

            count.Close(r.ApplyAdjustments); // valida que todos los productos estén auditados antes de continuar
            await _repo.UpdateAsync(count, ct);

            if (r.ApplyAdjustments)
            {
                foreach (var it in count.Items.Where(i => i.Difference != 0))
                {
                    var p = await _products.GetByIdAsync(t, it.ProductId, ct);
                    if (p is not null) { p.AdjustStock(count.WarehouseId, it.Difference); await _products.UpdateAsync(p, ct); }
                }
            }

            // Vincula el conteo con la(s) caja(s) abiertas del mismo almacén: quedan marcadas como
            // "conteo del turno realizado" para que el cierre de caja lo muestre.
            foreach (var register in await _registers.GetOpenByWarehouseAsync(t, count.WarehouseId, ct))
            {
                register.MarkInventoryCountCompleted();
                await _registers.UpdateAsync(register, ct);
            }

            await _uow.SaveChangesAsync(ct);
            return InventoryMapper.ToDto(count);
        }
    }

    public class SetInventoryCountItemAuditedHandler : IRequestHandler<SetInventoryCountItemAuditedCommand, InventoryCountDto>
    {
        private readonly IInventoryCountRepository _repo; private readonly IOperationsUnitOfWork _uow; private readonly ICurrentUserService _u;
        public SetInventoryCountItemAuditedHandler(IInventoryCountRepository repo, IOperationsUnitOfWork uow, ICurrentUserService u)
        { _repo = repo; _uow = uow; _u = u; }

        public async Task<InventoryCountDto> Handle(SetInventoryCountItemAuditedCommand r, CancellationToken ct)
        {
            var t = OpsMapper.RequireTenant(_u);
            var count = await _repo.GetByIdAsync(t, r.Id, ct) ?? throw new DomainException("Conteo no encontrado.");
            count.SetItemAudited(r.ProductId, r.Audited);
            await _repo.UpdateAsync(count, ct);
            await _uow.SaveChangesAsync(ct);
            return InventoryMapper.ToDto(count);
        }
    }

    public class GetInventoryCountsHandler : IRequestHandler<GetInventoryCountsQuery, IReadOnlyList<InventoryCountDto>>
    {
        private readonly IInventoryCountRepository _repo; private readonly ICurrentUserService _u;
        public GetInventoryCountsHandler(IInventoryCountRepository repo, ICurrentUserService u) { _repo = repo; _u = u; }
        public async Task<IReadOnlyList<InventoryCountDto>> Handle(GetInventoryCountsQuery r, CancellationToken ct)
        {
            var t = OpsMapper.RequireTenant(_u);
            return (await _repo.GetByTenantAsync(t, r.WarehouseId, ct)).Select(InventoryMapper.ToDto).ToList();
        }
    }

    public class GetInventoryCountByIdHandler : IRequestHandler<GetInventoryCountByIdQuery, InventoryCountDto>
    {
        private readonly IInventoryCountRepository _repo; private readonly ICurrentUserService _u;
        public GetInventoryCountByIdHandler(IInventoryCountRepository repo, ICurrentUserService u) { _repo = repo; _u = u; }
        public async Task<InventoryCountDto> Handle(GetInventoryCountByIdQuery r, CancellationToken ct)
        {
            var t = OpsMapper.RequireTenant(_u);
            var c = await _repo.GetByIdAsync(t, r.Id, ct) ?? throw new DomainException("Conteo no encontrado.");
            return InventoryMapper.ToDto(c);
        }
    }
}
