using BusinessSearcher.API.Common.Authorization;
using BusinessSearcher.API.Controllers;
using BusinessSearcher.Application.DTOs.Operations;
using BusinessSearcher.Application.Features.Operations.Categories;
using BusinessSearcher.Application.Features.Operations.Products;
using BusinessSearcher.Domain.BoundedContext.Operations.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BusinessSearcher.API.Controllers.v1
{
    /// <summary>Categorías de productos (TPV/ERP).</summary>
    [Authorize]
    [Route("api/v1/ops/categories")]
    public class OpsCategoriesController : BaseApiController
    {
        [HttpGet]
        public async Task<IActionResult> GetAll(CancellationToken ct)
            => Ok(await Mediator.Send(new GetCategoriesQuery(), ct));

        [HttpPost]
        [OpsRoles(OperationsRole.Almacenero, OperationsRole.JefeDeTurno)]
        public async Task<IActionResult> Create([FromBody] CreateCategoryDto dto, CancellationToken ct)
            => StatusCode(201, new { success = true, message = "Categoría creada.", data = await Mediator.Send(new CreateCategoryCommand(dto), ct) });

        [HttpPut("{id:guid}")]
        [OpsRoles(OperationsRole.Almacenero, OperationsRole.JefeDeTurno)]
        public async Task<IActionResult> Update(Guid id, [FromBody] CreateCategoryDto dto, CancellationToken ct)
            => Ok(await Mediator.Send(new UpdateCategoryCommand(id, dto), ct), "Categoría actualizada.");

        [HttpDelete("{id:guid}")]
        [OpsRoles(OperationsRole.Almacenero, OperationsRole.JefeDeTurno)]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            await Mediator.Send(new DeleteCategoryCommand(id), ct);
            return Ok<object?>(null, "Categoría eliminada.");
        }
    }

    /// <summary>Productos del catálogo, stock multi-almacén y doble moneda (TPV/ERP).</summary>
    [Authorize]
    [Route("api/v1/ops/products")]
    public class OpsProductsController : BaseApiController
    {
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] string? search, [FromQuery] Guid? categoryId,
            [FromQuery] Guid? warehouseId, [FromQuery] bool? lowStockOnly, CancellationToken ct)
            => Ok(await Mediator.Send(new GetProductsQuery(search, categoryId, warehouseId, lowStockOnly), ct));

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
            => Ok(await Mediator.Send(new GetProductByIdQuery(id), ct));

        [HttpGet("barcode/{barcode}")]
        public async Task<IActionResult> GetByBarcode(string barcode, CancellationToken ct)
            => Ok(await Mediator.Send(new GetProductByBarcodeQuery(barcode), ct));

        [HttpGet("{id:guid}/price-history")]
        public async Task<IActionResult> PriceHistory(Guid id, CancellationToken ct)
            => Ok(await Mediator.Send(new GetProductPriceHistoryQuery(id), ct));

        [HttpPost]
        [OpsRoles(OperationsRole.Almacenero, OperationsRole.JefeDeTurno)]
        public async Task<IActionResult> Create([FromBody] CreateProductDto dto, CancellationToken ct)
            => StatusCode(201, new { success = true, message = "Producto creado.", data = await Mediator.Send(new CreateProductCommand(dto), ct) });

        [HttpPut("{id:guid}")]
        [OpsRoles(OperationsRole.Almacenero, OperationsRole.JefeDeTurno)]
        public async Task<IActionResult> Update(Guid id, [FromBody] CreateProductDto dto, CancellationToken ct)
            => Ok(await Mediator.Send(new UpdateProductCommand(id, dto), ct), "Producto actualizado.");

        [HttpPost("{id:guid}/adjust-stock")]
        [OpsRoles(OperationsRole.Almacenero, OperationsRole.JefeDeTurno, OperationsRole.Cajero)]
        public async Task<IActionResult> AdjustStock(Guid id, [FromBody] AdjustStockDto dto, CancellationToken ct)
            => Ok(await Mediator.Send(new AdjustProductStockCommand(id, dto), ct), "Stock ajustado.");

        /// <summary>Marca o desmarca el producto como visible en la búsqueda pública de la app.</summary>
        [HttpPatch("{id:guid}/public-visibility")]
        [OpsRoles(OperationsRole.Almacenero, OperationsRole.JefeDeTurno)]
        public async Task<IActionResult> SetPublicVisibility(Guid id, [FromBody] SetProductPublicVisibilityDto dto, CancellationToken ct)
            => Ok(await Mediator.Send(new SetProductPublicVisibilityCommand(id, dto), ct), "Visibilidad actualizada.");

        /// <summary>
        /// Importa el catálogo desde Excel (Código, Producto, Categoría, Cant. disponible, Precio x unidad (USD);
        /// Descripción opcional). Crea categorías y productos nuevos; los códigos existentes no se duplican: su
        /// stock en el almacén indicado se ajusta al valor del archivo. Todo-o-nada ante errores de archivo.
        /// </summary>
        [HttpPost("import-excel")]
        [OpsRoles(OperationsRole.Almacenero, OperationsRole.JefeDeTurno)]
        [RequestSizeLimit(52_428_800)] // 50MB
        [RequestFormLimits(MultipartBodyLengthLimit = 52_428_800)] // el límite global de FormOptions es 5MB
        public async Task<IActionResult> Import(IFormFile file, [FromForm] Guid warehouseId,
            [FromForm] decimal? exchangeRate, [FromForm] string? catalogDate, [FromForm] string? priceDecisions,
            CancellationToken ct)
        {
            if (!System.DateOnly.TryParseExact(catalogDate, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var date))
                throw new BusinessSearcher.Domain.Exceptions.DomainException("La fecha del catálogo es obligatoria (formato yyyy-MM-dd).");

            IReadOnlyDictionary<string, string>? decisions = null;
            if (!string.IsNullOrWhiteSpace(priceDecisions))
            {
                try { decisions = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(priceDecisions); }
                catch (System.Text.Json.JsonException)
                { throw new BusinessSearcher.Domain.Exceptions.DomainException("Las decisiones de precio no tienen un formato válido."); }
            }

            var result = await Mediator.Send(new ImportProductsCatalogCommand(file, warehouseId, exchangeRate, date, decisions), ct);
            return Ok(result, result.NeedsDecision ? "Hay precios distintos: decide cuál usar." : "Catálogo importado.");
        }

        /// <summary>Sube o reemplaza la foto del producto.</summary>
        [HttpPost("{id:guid}/image")]
        [OpsRoles(OperationsRole.Almacenero, OperationsRole.JefeDeTurno)]
        [RequestSizeLimit(5_242_880)] // 5MB
        public async Task<IActionResult> UploadImage(Guid id, IFormFile file, CancellationToken ct)
        {
            var imageUrl = await Mediator.Send(new UploadProductImageCommand(id, file), ct);
            return Ok(new { imageUrl }, "Imagen subida exitosamente.");
        }

        [HttpDelete("{id:guid}")]
        [OpsRoles(OperationsRole.Almacenero, OperationsRole.JefeDeTurno)]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            await Mediator.Send(new DeleteProductCommand(id), ct);
            return Ok<object?>(null, "Producto eliminado.");
        }
    }

    /// <summary>Feed de notificaciones operativas: stock bajo + cambios de precio recientes.</summary>
    [Authorize]
    [Route("api/v1/ops/notifications")]
    public class OpsNotificationsController : BaseApiController
    {
        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] int lowStockLimit = 50, [FromQuery] int priceChangeHours = 24, CancellationToken ct = default)
            => Ok(await Mediator.Send(new GetOperationalNotificationsQuery(lowStockLimit, priceChangeHours), ct));

        /// <summary>Cantidad de notificaciones no vistas todavía por la cuenta autenticada.</summary>
        [HttpGet("unread-count")]
        public async Task<IActionResult> UnreadCount(
            [FromQuery] int lowStockLimit = 50, [FromQuery] int priceChangeHours = 24, CancellationToken ct = default)
            => Ok(await Mediator.Send(new GetNotificationsUnreadCountQuery(lowStockLimit, priceChangeHours), ct));

        /// <summary>Marca el feed de notificaciones como visto (limpia el contador pendiente).</summary>
        [HttpPost("mark-seen")]
        public async Task<IActionResult> MarkSeen(CancellationToken ct)
        {
            await Mediator.Send(new MarkNotificationsSeenCommand(), ct);
            return Ok<object?>(null, "Notificaciones marcadas como vistas.");
        }
    }
}
