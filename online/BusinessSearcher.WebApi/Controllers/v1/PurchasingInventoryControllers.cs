using BusinessSearcher.API.Common.Authorization;
using BusinessSearcher.API.Controllers;
using BusinessSearcher.Application.DTOs.Operations;
using BusinessSearcher.Application.Features.Operations.InventoryCounts;
using BusinessSearcher.Application.Features.Operations.InventoryMovements;
using BusinessSearcher.Application.Features.Operations.PurchaseRequests;
using BusinessSearcher.Application.Features.Operations.Purchases;
using BusinessSearcher.Domain.BoundedContext.Operations.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BusinessSearcher.API.Controllers.v1
{
    /// <summary>Solicitudes de compra / reabastecimiento (TPV/ERP).</summary>
    [Authorize]
    [Route("api/v1/ops/purchase-requests")]
    public class OpsPurchaseRequestsController : BaseApiController
    {
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] string? status, CancellationToken ct)
            => Ok(await Mediator.Send(new GetPurchaseRequestsQuery(status), ct));

        [HttpPost]
        [OpsRoles(OperationsRole.Almacenero, OperationsRole.JefeDeTurno)]
        public async Task<IActionResult> Create([FromBody] CreatePurchaseRequestDto dto, CancellationToken ct)
            => StatusCode(201, new { success = true, message = "Solicitud creada.", data = await Mediator.Send(new CreatePurchaseRequestCommand(dto), ct) });

        [HttpPost("generate-low-stock")]
        [OpsRoles(OperationsRole.Almacenero, OperationsRole.JefeDeTurno)]
        public async Task<IActionResult> GenerateLowStock(CancellationToken ct)
            => Ok(await Mediator.Send(new GenerateLowStockRequestsCommand(), ct), "Solicitudes generadas por bajo stock.");

        // Regla TPV: solo el Administrador ajusta cantidades / aprueba.
        [HttpPut("{id:guid}/quantity")]
        [OpsRoles]
        public async Task<IActionResult> UpdateQty(Guid id, [FromBody] UpdatePurchaseRequestQtyDto dto, CancellationToken ct)
            => Ok(await Mediator.Send(new UpdatePurchaseRequestQtyCommand(id, dto.RequestedQuantity), ct), "Cantidad actualizada.");

        [HttpPost("{id:guid}/approve")]
        [OpsRoles]
        public async Task<IActionResult> Approve(Guid id, CancellationToken ct)
            => Ok(await Mediator.Send(new ApprovePurchaseRequestCommand(id), ct), "Solicitud aprobada.");

        [HttpPost("{id:guid}/reject")]
        [OpsRoles]
        public async Task<IActionResult> Reject(Guid id, CancellationToken ct)
            => Ok(await Mediator.Send(new RejectPurchaseRequestCommand(id), ct), "Solicitud rechazada.");
    }

    /// <summary>Compras a proveedor (TPV/ERP). Al recibirlas aumentan el stock.</summary>
    [Authorize]
    [Route("api/v1/ops/purchases")]
    public class OpsPurchasesController : BaseApiController
    {
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] Guid? supplierId, CancellationToken ct)
            => Ok(await Mediator.Send(new GetPurchasesQuery(from, to, supplierId), ct));

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
            => Ok(await Mediator.Send(new GetPurchaseByIdQuery(id), ct));

        [HttpPost]
        [OpsRoles(OperationsRole.Comercial, OperationsRole.JefeDeTurno)]
        public async Task<IActionResult> Create([FromBody] CreatePurchaseDto dto, CancellationToken ct)
            => StatusCode(201, new { success = true, message = "Compra registrada.", data = await Mediator.Send(new CreatePurchaseCommand(dto), ct) });
    }

    /// <summary>Movimientos de inventario: entradas, salidas, traslados y mermas (TPV/ERP).</summary>
    [Authorize]
    [Route("api/v1/ops/inventory/movements")]
    public class OpsInventoryMovementsController : BaseApiController
    {
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] DateTime? from, [FromQuery] DateTime? to,
            [FromQuery] Guid? productId, [FromQuery] Guid? warehouseId, CancellationToken ct)
            => Ok(await Mediator.Send(new GetInventoryMovementsQuery(from, to, productId, warehouseId), ct));

        [HttpPost]
        [OpsRoles(OperationsRole.Almacenero, OperationsRole.JefeDeTurno)]
        public async Task<IActionResult> Create([FromBody] CreateInventoryMovementDto dto, CancellationToken ct)
            => StatusCode(201, new { success = true, message = "Movimiento registrado.", data = await Mediator.Send(new CreateInventoryMovementCommand(dto), ct) });

        /// <summary>Convierte/desglosa N productos origen en un producto destino con otra unidad (p.ej. comprar caja, vender por unidad).</summary>
        [HttpPost("convert")]
        [OpsRoles(OperationsRole.Almacenero, OperationsRole.JefeDeTurno)]
        public async Task<IActionResult> Convert([FromBody] ConvertInventoryDto dto, CancellationToken ct)
            => StatusCode(201, new { success = true, message = "Inventario convertido.", data = await Mediator.Send(new ConvertInventoryCommand(dto), ct) });
    }

    /// <summary>Conteo físico de inventario y reporte de discrepancias (TPV/ERP).</summary>
    [Authorize]
    [Route("api/v1/ops/inventory/counts")]
    public class OpsInventoryCountsController : BaseApiController
    {
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] Guid? warehouseId, CancellationToken ct)
            => Ok(await Mediator.Send(new GetInventoryCountsQuery(warehouseId), ct));

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
            => Ok(await Mediator.Send(new GetInventoryCountByIdQuery(id), ct));

        [HttpPost]
        [OpsRoles(OperationsRole.Cajero, OperationsRole.Almacenero, OperationsRole.JefeDeTurno)]
        public async Task<IActionResult> Create([FromBody] CreateInventoryCountDto dto, CancellationToken ct)
            => StatusCode(201, new { success = true, message = "Conteo registrado.", data = await Mediator.Send(new CreateInventoryCountCommand(dto), ct) });

        [HttpPost("{id:guid}/close")]
        [OpsRoles(OperationsRole.Almacenero, OperationsRole.JefeDeTurno)]
        public async Task<IActionResult> Close(Guid id, [FromBody] CloseInventoryCountDto dto, CancellationToken ct)
            => Ok(await Mediator.Send(new CloseInventoryCountCommand(id, dto.ApplyAdjustments), ct), "Conteo cerrado.");

        /// <summary>Marca/desmarca un producto del conteo como auditado (verificado físicamente).</summary>
        [HttpPatch("{id:guid}/items/{productId:guid}/audit")]
        [OpsRoles(OperationsRole.Cajero, OperationsRole.Almacenero, OperationsRole.JefeDeTurno)]
        public async Task<IActionResult> SetItemAudited(Guid id, Guid productId, [FromBody] SetInventoryCountItemAuditedDto dto, CancellationToken ct)
            => Ok(await Mediator.Send(new SetInventoryCountItemAuditedCommand(id, productId, dto.Audited), ct));
    }
}
