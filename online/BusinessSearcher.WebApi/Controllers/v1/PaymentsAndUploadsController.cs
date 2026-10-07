using BusinessSearcher.Application.Features.StoreManagement.Commands.Upload;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using BusinessSearcher.API.Controllers;

namespace BusinessSearcher.API.Controllers.v1
{
    // ── Uploads ───────────────────────────────────────────────────────────────────
    [Route("api/v1/uploads")]
    [Authorize]
    public class UploadsController : BaseApiController
    {
        /// <summary>Sube o reemplaza el logo de una tienda</summary>
        [HttpPost("stores/{storeId:guid}/logo")]
        [RequestSizeLimit(2_097_152)] // 2MB
        public async Task<IActionResult> UploadStoreLogo(
            Guid storeId, IFormFile file, CancellationToken ct)
        {
            var logoUrl = await Mediator.Send(
                new UploadStoreLogoCommand(storeId, file), ct);

            return Ok(new { logoUrl }, "Logo actualizado exitosamente.");
        }
    }
}
