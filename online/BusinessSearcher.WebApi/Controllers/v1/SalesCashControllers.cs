using BusinessSearcher.API.Common.Authorization;
using BusinessSearcher.API.Controllers;
using BusinessSearcher.Application.Commons.Interfaces;
using BusinessSearcher.Application.DTOs.Operations;
using BusinessSearcher.Application.Features.Operations.CashRegisters;
using BusinessSearcher.Application.Features.Operations.Sales;
using BusinessSearcher.Domain.BoundedContext.Operations.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BusinessSearcher.API.Controllers.v1
{
    /// <summary>Ventas / POS (TPV/ERP).</summary>
    [Authorize]
    [Route("api/v1/ops/sales")]
    public class OpsSalesController : BaseApiController
    {
        private readonly IOperationsReportExporter _exporter;
        public OpsSalesController(IOperationsReportExporter exporter) => _exporter = exporter;

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] DateTime? from, [FromQuery] DateTime? to,
            [FromQuery] Guid? registerId, [FromQuery] Guid? cashierId, [FromQuery] Guid? warehouseId, CancellationToken ct)
            => Ok(await Mediator.Send(new GetSalesQuery(from, to, registerId, cashierId, warehouseId), ct));

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
            => Ok(await Mediator.Send(new GetSaleByIdQuery(id), ct));

        /// <summary>
        /// Orden de entrega (.xlsx) de una o varias ventas seleccionadas: junta sus renglones en
        /// un solo documento descargable, con el formato de referencia del negocio.
        /// </summary>
        [HttpPost("orden-entrega")]
        public async Task<IActionResult> OrdenEntrega([FromBody] GenerateOrdenEntregaDto dto, CancellationToken ct)
        {
            var orden = await Mediator.Send(new GenerateOrdenEntregaQuery(dto), ct);
            var bytes = _exporter.ExportOrdenEntrega(orden);
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "orden-de-entrega.xlsx");
        }

        [HttpPost]
        [OpsRoles(OperationsRole.Cajero, OperationsRole.JefeDeTurno)]
        public async Task<IActionResult> Create([FromBody] CreateSaleDto dto, CancellationToken ct)
            => StatusCode(201, new { success = true, message = "Venta registrada.", data = await Mediator.Send(new CreateSaleCommand(dto), ct) });

        /// <summary>Edita los renglones de una venta ya registrada (reajusta stock). Solo Administrador.</summary>
        [HttpPut("{id:guid}")]
        [OpsRoles]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSaleDto dto, CancellationToken ct)
            => Ok(await Mediator.Send(new UpdateSaleCommand(id, dto), ct), "Venta actualizada.");

        [HttpPost("{id:guid}/refund")]
        [OpsRoles(OperationsRole.JefeDeTurno)]
        public async Task<IActionResult> Refund(Guid id, CancellationToken ct)
            => Ok(await Mediator.Send(new RefundSaleCommand(id), ct), "Venta reembolsada.");

        /// <summary>
        /// Importa ventas desde un Excel (Código, Producto, Cantidad, Precio). Cada fila descuenta
        /// stock real y entra a la caja abierta del usuario. Si algún precio del Excel difiere del
        /// precio del sistema y no se envía <paramref name="acceptNewPrices"/>, no registra nada
        /// todavía: devuelve las diferencias para que el cliente confirme y reenvíe la importación.
        /// </summary>
        [HttpPost("import")]
        [OpsRoles(OperationsRole.Cajero, OperationsRole.JefeDeTurno)]
        [RequestSizeLimit(5_242_880)] // 5MB
        public async Task<IActionResult> Import(IFormFile file, [FromForm] bool? acceptNewPrices, CancellationToken ct)
        {
            var result = await Mediator.Send(new ImportSalesCommand(file, acceptNewPrices), ct);
            var message = result.NeedsPriceConfirmation
                ? "Hay precios del Excel distintos a los del sistema. Confirma si deseas actualizarlos."
                : result.Success
                    ? $"Ventas importadas exitosamente ({result.ImportedCount})."
                    : "El archivo contiene errores. Revisa el detalle.";
            return Ok(result, message);
        }
    }

    /// <summary>Caja / arqueo (TPV/ERP).</summary>
    [Authorize]
    [Route("api/v1/ops/cash-registers")]
    public class OpsCashRegistersController : BaseApiController
    {
        [HttpGet("current")]
        public async Task<IActionResult> Current(CancellationToken ct)
            => Ok(await Mediator.Send(new GetCurrentCashRegisterQuery(), ct));

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
            => Ok(await Mediator.Send(new GetCashRegistersQuery(from, to), ct));

        [HttpGet("{id:guid}/movements")]
        public async Task<IActionResult> Movements(Guid id, CancellationToken ct)
            => Ok(await Mediator.Send(new GetCashMovementsQuery(id), ct));

        [HttpPost("open")]
        [OpsRoles(OperationsRole.Cajero, OperationsRole.JefeDeTurno)]
        public async Task<IActionResult> Open([FromBody] OpenCashRegisterDto dto, CancellationToken ct)
            => StatusCode(201, new { success = true, message = "Caja abierta.", data = await Mediator.Send(new OpenCashRegisterCommand(dto), ct) });

        [HttpPost("{id:guid}/close")]
        [OpsRoles(OperationsRole.Cajero, OperationsRole.JefeDeTurno)]
        public async Task<IActionResult> Close(Guid id, [FromBody] CloseCashRegisterDto dto, CancellationToken ct)
            => Ok(await Mediator.Send(new CloseCashRegisterCommand(id, dto), ct), "Caja cerrada.");

        [HttpPost("movements")]
        [OpsRoles(OperationsRole.Cajero, OperationsRole.JefeDeTurno)]
        public async Task<IActionResult> AddMovement([FromBody] CreateCashMovementDto dto, CancellationToken ct)
            => StatusCode(201, new { success = true, message = "Movimiento registrado.", data = await Mediator.Send(new CreateCashMovementCommand(dto), ct) });
    }
}
