using BusinessSearcher.API.Common.Authorization;
using BusinessSearcher.API.Controllers;
using BusinessSearcher.Application.Commons.Interfaces;
using BusinessSearcher.Application.DTOs.Operations;
using BusinessSearcher.Application.Features.Operations.AuditLogs;
using BusinessSearcher.Application.Features.Operations.BusinessInfoFeature;
using BusinessSearcher.Application.Features.Operations.Reports;
using BusinessSearcher.Application.Features.Operations.Settings;
using BusinessSearcher.Domain.BoundedContext.Operations.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BusinessSearcher.API.Controllers.v1
{
    /// <summary>Datos fiscales del negocio (TPV/ERP). Uno por negocio.</summary>
    [Authorize]
    [Route("api/v1/ops/business-info")]
    public class OpsBusinessInfoController : BaseApiController
    {
        [HttpGet]
        public async Task<IActionResult> Get(CancellationToken ct)
            => Ok(await Mediator.Send(new GetBusinessInfoQuery(), ct));

        [HttpPut]
        [OpsRoles]
        public async Task<IActionResult> Save([FromBody] SaveBusinessInfoDto dto, CancellationToken ct)
            => Ok(await Mediator.Send(new SaveBusinessInfoCommand(dto), ct), "Datos del negocio guardados.");
    }

    /// <summary>Auditoría de seguridad del TPV: quién hizo qué, cuándo y con qué resultado.</summary>
    [Authorize]
    [OpsRoles(OperationsRole.Auditor)]
    [Route("api/v1/ops/audit-log")]
    public class OpsAuditLogController : BaseApiController
    {
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] DateTime? from, [FromQuery] DateTime? to,
            [FromQuery] Guid? userId, [FromQuery] string? action, [FromQuery] int limit = 200, CancellationToken ct = default)
            => Ok(await Mediator.Send(new GetAuditLogsQuery(from, to, userId, action, limit), ct));
    }

    /// <summary>Configuración genérica clave-valor del negocio (flags/parámetros varios).</summary>
    [Authorize]
    [Route("api/v1/ops/settings")]
    public class OpsSettingsController : BaseApiController
    {
        [HttpGet]
        public async Task<IActionResult> GetAll(CancellationToken ct)
            => Ok(await Mediator.Send(new GetSettingsQuery(), ct));

        [HttpGet("{key}")]
        public async Task<IActionResult> Get(string key, CancellationToken ct)
            => Ok(await Mediator.Send(new GetSettingQuery(key), ct));

        [HttpPut("{key}")]
        [OpsRoles]
        public async Task<IActionResult> Save(string key, [FromBody] SaveSettingDto dto, CancellationToken ct)
            => Ok(await Mediator.Send(new SaveSettingCommand(key, dto), ct), "Configuración guardada.");
    }

    /// <summary>Reportes del TPV/ERP (ventas, inventario, gastos) con exportación a Excel.</summary>
    [Authorize]
    [OpsRoles(OperationsRole.Auditor, OperationsRole.JefeDeTurno)]
    [Route("api/v1/ops/reports")]
    public class OpsReportsController : BaseApiController
    {
        private readonly IOperationsReportExporter _exporter;
        public OpsReportsController(IOperationsReportExporter exporter) => _exporter = exporter;

        /// <summary>
        /// Dashboard mensual del año indicado (o el actual si no se especifica): ventas,
        /// compras, productos merma y ganancia mes a mes, con comparación contra el año anterior.
        /// </summary>
        [HttpGet("dashboard")]
        public async Task<IActionResult> Dashboard([FromQuery] int? year, [FromQuery] Guid? warehouseId, CancellationToken ct)
            => Ok(await Mediator.Send(new GetMonthlyDashboardQuery(year, warehouseId), ct));

        [HttpGet("sales")]
        public async Task<IActionResult> Sales([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] Guid? warehouseId, CancellationToken ct)
            => Ok(await Mediator.Send(new GetSalesReportQuery(from, to, warehouseId), ct));

        [HttpGet("sales/export")]
        public async Task<IActionResult> ExportSales([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] Guid? warehouseId, CancellationToken ct)
        {
            var report = await Mediator.Send(new GetSalesReportQuery(from, to, warehouseId), ct);
            var bytes = _exporter.ExportSales(report);
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "reporte-ventas.xlsx");
        }

        [HttpGet("inventory")]
        public async Task<IActionResult> Inventory([FromQuery] bool? lowStockOnly, [FromQuery] Guid? warehouseId, CancellationToken ct)
            => Ok(await Mediator.Send(new GetInventoryReportQuery(lowStockOnly, warehouseId), ct));

        [HttpGet("inventory/export")]
        public async Task<IActionResult> ExportInventory([FromQuery] bool? lowStockOnly, [FromQuery] Guid? warehouseId, CancellationToken ct)
        {
            var report = await Mediator.Send(new GetInventoryReportQuery(lowStockOnly, warehouseId), ct);
            var bytes = _exporter.ExportInventory(report);
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "reporte-inventario.xlsx");
        }

        [HttpGet("expenses")]
        public async Task<IActionResult> Expenses([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
            => Ok(await Mediator.Send(new GetExpensesReportQuery(from, to), ct));

        [HttpGet("expenses/export")]
        public async Task<IActionResult> ExportExpenses([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
        {
            var report = await Mediator.Send(new GetExpensesReportQuery(from, to), ct);
            var bytes = _exporter.ExportExpenses(report);
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "reporte-gastos.xlsx");
        }
    }
}
