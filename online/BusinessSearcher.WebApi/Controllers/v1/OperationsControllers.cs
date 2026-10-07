using BusinessSearcher.API.Common.Authorization;
using BusinessSearcher.API.Controllers;
using BusinessSearcher.Application.DTOs.Operations;
using BusinessSearcher.Application.Features.Operations.ExchangeRates;
using BusinessSearcher.Application.Features.Operations.Expenses;
using BusinessSearcher.Application.Features.Operations.Payroll;
using BusinessSearcher.Application.Features.Operations.RoleSalaryConfigs;
using BusinessSearcher.Application.Features.Operations.Suppliers;
using BusinessSearcher.Application.Features.Operations.Warehouses;
using BusinessSearcher.Domain.BoundedContext.Operations.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BusinessSearcher.API.Controllers.v1
{
    /// <summary>Proveedores (TPV/ERP). Requiere sesión de negocio (Tenant).</summary>
    [Authorize]
    [Route("api/v1/ops/suppliers")]
    public class OpsSuppliersController : BaseApiController
    {
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] string? search, CancellationToken ct)
            => Ok(await Mediator.Send(new GetSuppliersQuery(search), ct));

        [HttpPost]
        [OpsRoles(OperationsRole.Comercial)]
        public async Task<IActionResult> Create([FromBody] CreateSupplierDto dto, CancellationToken ct)
            => StatusCode(201, new { success = true, message = "Proveedor creado.", data = await Mediator.Send(new CreateSupplierCommand(dto), ct) });

        [HttpPut("{id:guid}")]
        [OpsRoles(OperationsRole.Comercial)]
        public async Task<IActionResult> Update(Guid id, [FromBody] CreateSupplierDto dto, CancellationToken ct)
            => Ok(await Mediator.Send(new UpdateSupplierCommand(id, dto), ct), "Proveedor actualizado.");

        [HttpDelete("{id:guid}")]
        [OpsRoles(OperationsRole.Comercial)]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            await Mediator.Send(new DeleteSupplierCommand(id), ct);
            return Ok<object?>(null, "Proveedor eliminado.");
        }
    }

    /// <summary>Almacenes (TPV/ERP).</summary>
    [Authorize]
    [Route("api/v1/ops/warehouses")]
    public class OpsWarehousesController : BaseApiController
    {
        [HttpGet]
        public async Task<IActionResult> GetAll(CancellationToken ct)
            => Ok(await Mediator.Send(new GetWarehousesQuery(), ct));

        [HttpPost]
        [OpsRoles(OperationsRole.Almacenero, OperationsRole.JefeDeTurno)]
        public async Task<IActionResult> Create([FromBody] CreateWarehouseDto dto, CancellationToken ct)
            => StatusCode(201, new { success = true, message = "Almacén creado.", data = await Mediator.Send(new CreateWarehouseCommand(dto), ct) });

        [HttpPut("{id:guid}")]
        [OpsRoles(OperationsRole.Almacenero, OperationsRole.JefeDeTurno)]
        public async Task<IActionResult> Update(Guid id, [FromBody] CreateWarehouseDto dto, CancellationToken ct)
            => Ok(await Mediator.Send(new UpdateWarehouseCommand(id, dto), ct), "Almacén actualizado.");
    }

    /// <summary>Gastos operativos (TPV/ERP).</summary>
    [Authorize]
    [Route("api/v1/ops/expenses")]
    public class OpsExpensesController : BaseApiController
    {
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
            => Ok(await Mediator.Send(new GetExpensesQuery(from, to), ct));

        [HttpPost]
        [OpsRoles(OperationsRole.Comercial, OperationsRole.JefeDeTurno)]
        public async Task<IActionResult> Create([FromBody] CreateExpenseDto dto, CancellationToken ct)
            => StatusCode(201, new { success = true, message = "Gasto registrado.", data = await Mediator.Send(new CreateExpenseCommand(dto), ct) });
    }

    /// <summary>Tasa de cambio CUP/USD (TPV/ERP).</summary>
    [Authorize]
    [Route("api/v1/ops/exchange-rate")]
    public class OpsExchangeRateController : BaseApiController
    {
        [HttpGet]
        public async Task<IActionResult> Current(CancellationToken ct)
            => Ok(await Mediator.Send(new GetCurrentExchangeRateQuery(), ct));

        [HttpGet("history")]
        public async Task<IActionResult> History([FromQuery] int limit = 30, CancellationToken ct = default)
            => Ok(await Mediator.Send(new GetExchangeRateHistoryQuery(limit), ct));

        [HttpPost]
        [OpsRoles]
        public async Task<IActionResult> Set([FromBody] SetExchangeRateDto dto, CancellationToken ct)
            => StatusCode(201, new { success = true, message = "Tasa actualizada.", data = await Mediator.Send(new SetExchangeRateCommand(dto.Rate), ct) });
    }

    /// <summary>Salario fijo y % de venta por rol operativo (TPV/ERP). Solo el Administrador puede leer,
    /// modificar o eliminar esta configuración.</summary>
    [Authorize]
    [OpsRoles]
    [Route("api/v1/ops/role-salary-configs")]
    public class OpsRoleSalaryConfigsController : BaseApiController
    {
        [HttpGet]
        public async Task<IActionResult> GetAll(CancellationToken ct)
            => Ok(await Mediator.Send(new GetRoleSalaryConfigsQuery(), ct));

        [HttpPut("{role}")]
        public async Task<IActionResult> Save(OperationsRole role, [FromBody] SaveRoleSalaryConfigDto dto, CancellationToken ct)
            => Ok(await Mediator.Send(new SaveRoleSalaryConfigCommand(role, dto), ct), "Configuración del rol guardada.");

        [HttpDelete("{role}")]
        public async Task<IActionResult> Delete(OperationsRole role, CancellationToken ct)
        {
            await Mediator.Send(new DeleteRoleSalaryConfigCommand(role), ct);
            return Ok<object?>(null, "Configuración del rol eliminada.");
        }
    }

    /// <summary>Nómina: previsualiza y registra el gasto de salario calculado a partir de los roles
    /// configurados, el mínimo exento y las ventas de cada trabajador en el periodo (TPV/ERP).</summary>
    [Authorize]
    [Route("api/v1/ops/payroll")]
    public class OpsPayrollController : BaseApiController
    {
        [HttpGet("preview")]
        [OpsRoles(OperationsRole.Comercial, OperationsRole.JefeDeTurno)]
        public async Task<IActionResult> Preview([FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken ct)
            => Ok(await Mediator.Send(new GetPayrollPreviewQuery(from, to), ct));

        [HttpPost("register")]
        [OpsRoles(OperationsRole.Comercial, OperationsRole.JefeDeTurno)]
        public async Task<IActionResult> Register([FromBody] RegisterPayrollExpenseDto dto, CancellationToken ct)
            => StatusCode(201, new { success = true, message = "Gasto de nómina registrado.", data = await Mediator.Send(new RegisterPayrollExpenseCommand(dto), ct) });
    }
}
