using BusinessSearcher.Application.Commons.Interfaces;
using BusinessSearcher.Application.DTOs.Operations;
using BusinessSearcher.Domain.BoundedContext.Operations.Aggregates;
using BusinessSearcher.Domain.BoundedContext.Operations.Enums;
using BusinessSearcher.Domain.BoundedContext.Operations.Repositories;
using BusinessSearcher.Domain.Exceptions;
using MediatR;

namespace BusinessSearcher.Application.Features.Operations
{
    internal static class PurchasingMapper
    {
        public static PurchaseRequestDto ToDto(PurchaseRequest r) => new(
            r.Id, r.ProductId, r.ProductName, r.WarehouseId, r.CurrentStock, r.MinStock,
            r.RequestedQuantity, r.Status.ToString(), r.UserId, r.Notes, r.Date);

        public static PurchaseItemDto ToDto(PurchaseItem i) => new(
            i.ProductId, i.ProductName, i.Quantity, i.CostPrice, i.LineTotal, i.BatchNumber, i.ExpirationDate);

        public static PurchaseDto ToDto(Purchase p) => new(
            p.Id, p.SupplierId, p.WarehouseId, p.Total, p.TotalUSD, p.AssociatedExpenses, p.Currency.ToString(),
            p.ExchangeRate, p.Status.ToString(), p.InvoiceUrl, p.Date,
            p.Items.Select(ToDto).ToList(), p.PurchaseRequestIds.ToList());
    }
}

// ── Solicitudes de compra ──
namespace BusinessSearcher.Application.Features.Operations.PurchaseRequests
{
    public record CreatePurchaseRequestCommand(CreatePurchaseRequestDto Dto) : IRequest<PurchaseRequestDto>;
    public record GenerateLowStockRequestsCommand : IRequest<IReadOnlyList<PurchaseRequestDto>>;
    public record UpdatePurchaseRequestQtyCommand(Guid Id, int RequestedQuantity) : IRequest<PurchaseRequestDto>;
    public record ApprovePurchaseRequestCommand(Guid Id) : IRequest<PurchaseRequestDto>;
    public record RejectPurchaseRequestCommand(Guid Id) : IRequest<PurchaseRequestDto>;
    public record GetPurchaseRequestsQuery(string? Status) : IRequest<IReadOnlyList<PurchaseRequestDto>>;

    public class CreatePurchaseRequestHandler : IRequestHandler<CreatePurchaseRequestCommand, PurchaseRequestDto>
    {
        private readonly IPurchaseRequestRepository _repo; private readonly IProductRepository _products;
        private readonly IOperationsUnitOfWork _uow; private readonly ICurrentUserService _u;
        public CreatePurchaseRequestHandler(IPurchaseRequestRepository repo, IProductRepository products, IOperationsUnitOfWork uow, ICurrentUserService u)
        { _repo = repo; _products = products; _uow = uow; _u = u; }
        public async Task<PurchaseRequestDto> Handle(CreatePurchaseRequestCommand r, CancellationToken ct)
        {
            var t = OpsMapper.RequireTenant(_u); var d = r.Dto;
            var p = await _products.GetByIdAsync(t, d.ProductId, ct) ?? throw new DomainException("Producto no encontrado.");
            var current = p.StockFor(d.WarehouseId).Quantity;
            var req = PurchaseRequest.Create(t, p.Id, p.Name, d.WarehouseId, current, p.MinStock, d.RequestedQuantity, _u.AccountId, d.Notes);
            await _repo.AddAsync(req, ct); await _uow.SaveChangesAsync(ct);
            return PurchasingMapper.ToDto(req);
        }
    }

    public class GenerateLowStockRequestsHandler : IRequestHandler<GenerateLowStockRequestsCommand, IReadOnlyList<PurchaseRequestDto>>
    {
        private readonly IPurchaseRequestRepository _repo; private readonly IProductRepository _products;
        private readonly IOperationsUnitOfWork _uow; private readonly ICurrentUserService _u;
        public GenerateLowStockRequestsHandler(IPurchaseRequestRepository repo, IProductRepository products, IOperationsUnitOfWork uow, ICurrentUserService u)
        { _repo = repo; _products = products; _uow = uow; _u = u; }
        public async Task<IReadOnlyList<PurchaseRequestDto>> Handle(GenerateLowStockRequestsCommand r, CancellationToken ct)
        {
            var t = OpsMapper.RequireTenant(_u);
            var products = await _products.GetByTenantAsync(t, null, null, null, null, ct);
            var created = new List<PurchaseRequest>();
            foreach (var p in products.Where(p => p.MinStock > 0))
            {
                foreach (var s in p.Stocks.Where(s => s.Quantity <= p.MinStock))
                {
                    if (await _repo.HasPendingForProductAsync(t, p.Id, s.WarehouseId, ct)) continue;
                    var qty = Math.Max(p.MinStock - s.Quantity, 1); // reponer al menos hasta el mínimo
                    var req = PurchaseRequest.Create(t, p.Id, p.Name, s.WarehouseId, s.Quantity, p.MinStock, qty, _u.AccountId,
                        "Generada automáticamente por bajo stock.");
                    await _repo.AddAsync(req, ct);
                    created.Add(req);
                }
            }
            if (created.Count > 0) await _uow.SaveChangesAsync(ct);
            return created.Select(PurchasingMapper.ToDto).ToList();
        }
    }

    public class UpdatePurchaseRequestQtyHandler : IRequestHandler<UpdatePurchaseRequestQtyCommand, PurchaseRequestDto>
    {
        private readonly IPurchaseRequestRepository _repo; private readonly IOperationsUnitOfWork _uow; private readonly ICurrentUserService _u;
        public UpdatePurchaseRequestQtyHandler(IPurchaseRequestRepository repo, IOperationsUnitOfWork uow, ICurrentUserService u) { _repo = repo; _uow = uow; _u = u; }
        public async Task<PurchaseRequestDto> Handle(UpdatePurchaseRequestQtyCommand r, CancellationToken ct)
        {
            var t = OpsMapper.RequireTenant(_u);
            var req = await _repo.GetByIdAsync(t, r.Id, ct) ?? throw new DomainException("Solicitud no encontrada.");
            req.UpdateQuantity(r.RequestedQuantity);
            await _repo.UpdateAsync(req, ct); await _uow.SaveChangesAsync(ct);
            return PurchasingMapper.ToDto(req);
        }
    }

    public class ApprovePurchaseRequestHandler : IRequestHandler<ApprovePurchaseRequestCommand, PurchaseRequestDto>
    {
        private readonly IPurchaseRequestRepository _repo; private readonly IOperationsUnitOfWork _uow; private readonly ICurrentUserService _u;
        public ApprovePurchaseRequestHandler(IPurchaseRequestRepository repo, IOperationsUnitOfWork uow, ICurrentUserService u) { _repo = repo; _uow = uow; _u = u; }
        public async Task<PurchaseRequestDto> Handle(ApprovePurchaseRequestCommand r, CancellationToken ct)
        {
            var t = OpsMapper.RequireTenant(_u);
            var req = await _repo.GetByIdAsync(t, r.Id, ct) ?? throw new DomainException("Solicitud no encontrada.");
            req.Approve(); await _repo.UpdateAsync(req, ct); await _uow.SaveChangesAsync(ct);
            return PurchasingMapper.ToDto(req);
        }
    }

    public class RejectPurchaseRequestHandler : IRequestHandler<RejectPurchaseRequestCommand, PurchaseRequestDto>
    {
        private readonly IPurchaseRequestRepository _repo; private readonly IOperationsUnitOfWork _uow; private readonly ICurrentUserService _u;
        public RejectPurchaseRequestHandler(IPurchaseRequestRepository repo, IOperationsUnitOfWork uow, ICurrentUserService u) { _repo = repo; _uow = uow; _u = u; }
        public async Task<PurchaseRequestDto> Handle(RejectPurchaseRequestCommand r, CancellationToken ct)
        {
            var t = OpsMapper.RequireTenant(_u);
            var req = await _repo.GetByIdAsync(t, r.Id, ct) ?? throw new DomainException("Solicitud no encontrada.");
            req.Reject(); await _repo.UpdateAsync(req, ct); await _uow.SaveChangesAsync(ct);
            return PurchasingMapper.ToDto(req);
        }
    }

    public class GetPurchaseRequestsHandler : IRequestHandler<GetPurchaseRequestsQuery, IReadOnlyList<PurchaseRequestDto>>
    {
        private readonly IPurchaseRequestRepository _repo; private readonly ICurrentUserService _u;
        public GetPurchaseRequestsHandler(IPurchaseRequestRepository repo, ICurrentUserService u) { _repo = repo; _u = u; }
        public async Task<IReadOnlyList<PurchaseRequestDto>> Handle(GetPurchaseRequestsQuery r, CancellationToken ct)
        {
            var t = OpsMapper.RequireTenant(_u);
            PurchaseRequestStatus? status = Enum.TryParse<PurchaseRequestStatus>(r.Status, true, out var s) ? s : null;
            return (await _repo.GetByTenantAsync(t, status, ct)).Select(PurchasingMapper.ToDto).ToList();
        }
    }
}

// ── Compras ──
namespace BusinessSearcher.Application.Features.Operations.Purchases
{
    public record CreatePurchaseCommand(CreatePurchaseDto Dto) : IRequest<PurchaseDto>;
    public record GetPurchasesQuery(DateTime? From, DateTime? To, Guid? SupplierId) : IRequest<IReadOnlyList<PurchaseDto>>;
    public record GetPurchaseByIdQuery(Guid Id) : IRequest<PurchaseDto>;

    public class CreatePurchaseHandler : IRequestHandler<CreatePurchaseCommand, PurchaseDto>
    {
        private readonly IPurchaseRepository _purchases; private readonly IProductRepository _products;
        private readonly IPurchaseRequestRepository _requests; private readonly IProductPriceHistoryRepository _priceHistory;
        private readonly IOperationsUnitOfWork _uow; private readonly ICurrentUserService _u;
        public CreatePurchaseHandler(IPurchaseRepository purchases, IProductRepository products,
            IPurchaseRequestRepository requests, IProductPriceHistoryRepository priceHistory, IOperationsUnitOfWork uow, ICurrentUserService u)
        { _purchases = purchases; _products = products; _requests = requests; _priceHistory = priceHistory; _uow = uow; _u = u; }

        public async Task<PurchaseDto> Handle(CreatePurchaseCommand r, CancellationToken ct)
        {
            var t = OpsMapper.RequireTenant(_u); var d = r.Dto;
            if (d.Items is null || d.Items.Count == 0) throw new DomainException("La compra debe tener al menos un producto.");
            var currency = SalesMapper.ParseEnum(d.Currency, Currency.CUP);

            // Prorratea los gastos asociados (flete, aduana, etc.) entre las líneas según su
            // peso en el subtotal, para que el costo unitario final del producto los absorba.
            var subtotal = d.Items.Sum(i => i.Quantity * i.CostPrice);

            var items = new List<PurchaseItem>();
            foreach (var it in d.Items)
            {
                Product? p = it.ProductId.HasValue
                    ? await _products.GetByIdAsync(t, it.ProductId.Value, ct) ?? throw new DomainException($"Producto {it.ProductId} no encontrado.")
                    : null;

                if (p is null)
                {
                    // Alta de producto "al vuelo": si viene un barcode que ya existe, se reutiliza
                    // ese producto en vez de crear uno duplicado.
                    if (it.NewProduct is null)
                        throw new DomainException("Cada línea de compra requiere un producto existente o los datos de un producto nuevo.");
                    var np = it.NewProduct;
                    if (!string.IsNullOrWhiteSpace(np.Barcode))
                        p = await _products.GetByBarcodeAsync(t, np.Barcode, ct);
                    if (p is null)
                    {
                        p = Product.Create(t, np.Name, it.CostPrice, it.NewSellPrice ?? 0, np.Barcode, unit: np.Unit,
                            categoryId: np.CategoryId, sellPriceUsd: np.SellPriceUSD, minStock: np.MinStock, taxRate: np.TaxRate,
                            batchNumber: it.BatchNumber, expirationDate: it.ExpirationDate);
                        await _products.AddAsync(p, ct);
                    }
                }

                var lineSubtotal = it.Quantity * it.CostPrice;
                var expenseShare = d.AssociatedExpenses > 0 && subtotal > 0
                    ? d.AssociatedExpenses * (lineSubtotal / subtotal)
                    : 0m;
                var effectiveUnitCost = it.CostPrice + (expenseShare / it.Quantity);

                // Costo promedio ponderado: combina el stock ya existente (a su costo actual)
                // con la mercancía entrante (a su costo efectivo, con gastos ya prorrateados).
                var oldStock = p.TotalStock;
                var newCost = oldStock > 0
                    ? Math.Round(((oldStock * p.CostPrice) + (it.Quantity * effectiveUnitCost)) / (oldStock + it.Quantity), 4)
                    : Math.Round(effectiveUnitCost, 4);

                p.AdjustStock(d.WarehouseId, it.Quantity); // recibir mercancía → aumenta stock

                var oldCost = p.CostPrice; var oldSell = p.SellPrice;
                var oldCostUsd = p.CostPriceUSD; var oldSellUsd = p.SellPriceUSD;
                var newSell = it.NewSellPrice ?? p.SellPrice;

                // Actualiza costo (promedio ponderado) y (opcional) precio de venta del producto.
                p.Update(p.Name, newCost, newSell, p.Barcode, p.Description, p.Unit,
                    p.CategoryId, p.CostPriceUSD, p.SellPriceUSD, p.MinStock, p.TaxRate, it.BatchNumber ?? p.BatchNumber,
                    it.ExpirationDate ?? p.ExpirationDate, p.ForSale);
                await _products.UpdateAsync(p, ct);

                if (oldCost != newCost || oldSell != newSell)
                    await _priceHistory.AddAsync(ProductPriceHistory.Create(t, p.Id, oldCost, newCost, oldSell, newSell,
                        oldCostUsd, oldCostUsd, oldSellUsd, oldSellUsd, _u.AccountId, "Compra de mercancía"), ct);

                items.Add(PurchaseItem.Create(p.Id, p.Name, it.Quantity, it.CostPrice, it.BatchNumber, it.ExpirationDate));
            }

            var purchase = Purchase.Create(t, d.WarehouseId, items, currency, d.SupplierId, _u.AccountId,
                d.AssociatedExpenses, null, d.ExchangeRate, d.InvoiceUrl, d.PurchaseRequestIds);
            await _purchases.AddAsync(purchase, ct);

            // Marca como finalizadas las solicitudes asociadas.
            foreach (var reqId in d.PurchaseRequestIds ?? new List<Guid>())
            {
                var req = await _requests.GetByIdAsync(t, reqId, ct);
                if (req is not null) { req.Complete(); await _requests.UpdateAsync(req, ct); }
            }

            await _uow.SaveChangesAsync(ct);
            return PurchasingMapper.ToDto(purchase);
        }
    }

    public class GetPurchasesHandler : IRequestHandler<GetPurchasesQuery, IReadOnlyList<PurchaseDto>>
    {
        private readonly IPurchaseRepository _repo; private readonly ICurrentUserService _u;
        public GetPurchasesHandler(IPurchaseRepository repo, ICurrentUserService u) { _repo = repo; _u = u; }
        public async Task<IReadOnlyList<PurchaseDto>> Handle(GetPurchasesQuery r, CancellationToken ct)
        {
            var t = OpsMapper.RequireTenant(_u);
            return (await _repo.GetByTenantAsync(t, r.From, OpsMapper.EndOfDay(r.To), r.SupplierId, ct)).Select(PurchasingMapper.ToDto).ToList();
        }
    }

    public class GetPurchaseByIdHandler : IRequestHandler<GetPurchaseByIdQuery, PurchaseDto>
    {
        private readonly IPurchaseRepository _repo; private readonly ICurrentUserService _u;
        public GetPurchaseByIdHandler(IPurchaseRepository repo, ICurrentUserService u) { _repo = repo; _u = u; }
        public async Task<PurchaseDto> Handle(GetPurchaseByIdQuery r, CancellationToken ct)
        {
            var t = OpsMapper.RequireTenant(_u);
            var p = await _repo.GetByIdAsync(t, r.Id, ct) ?? throw new DomainException("Compra no encontrada.");
            return PurchasingMapper.ToDto(p);
        }
    }
}
