using BusinessSearcher.Application.Features.StoreManagement.Commands.Catalog;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using BusinessSearcher.API.Controllers;

namespace BusinessSearcher.API.Controllers.v1
{
    /// <summary>Importación de catálogo mayorista vía Excel (.xlsx)</summary>
    [Authorize]
    [Route("api/v1/ops/products")]
    public class CatalogController : BaseApiController
    {
        /// <summary>
        /// Importa un Excel (Producto, Cantidad, Precio, Venta Minima) que reemplaza
        /// el catálogo público (mayorista) del negocio autenticado.
        /// </summary>
        [HttpPost("import")]
        [RequestSizeLimit(5_242_880)] // 5MB
        [ProducesResponseType(typeof(object), 200)]
        public async Task<IActionResult> Import(
            IFormFile file, [FromForm] string currency, CancellationToken cancellationToken)
        {
            var result = await Mediator.Send(
                new ImportWholesaleCatalogCommand(file, currency), cancellationToken);

            return Ok(result, result.Success
                ? $"Catálogo importado exitosamente ({result.ImportedCount} productos)."
                : "El archivo contiene errores. Revisa el detalle.");
        }
    }
}
