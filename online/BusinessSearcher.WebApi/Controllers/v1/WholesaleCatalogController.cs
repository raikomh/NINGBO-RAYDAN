using BusinessSearcher.Application.Features.StoreManagement.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using BusinessSearcher.API.Controllers;

namespace BusinessSearcher.API.Controllers.v1
{
    /// <summary>Directorio de mayoristas, visible solo para tenants Minorista autenticados</summary>
    [Authorize]
    [Route("api/v1/wholesale-catalog")]
    public class WholesaleCatalogController : BaseApiController
    {
        [HttpGet]
        public async Task<IActionResult> Get(
            [FromQuery] string? q,
            [FromQuery] string? city,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            CancellationToken cancellationToken = default)
        {
            var result = await Mediator.Send(
                new GetWholesaleCatalogQuery(q, city, page, pageSize), cancellationToken);
            return Ok(result);
        }
    }
}
