using BusinessSearcher.Application.Commons.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BusinessSearcher.API.Controllers.v1
{
    /// <summary>
    /// Sirve archivos subidos (fotos de producto) a través del backend en vez de exponer el
    /// bucket de almacenamiento directamente: así el bucket puede quedarse privado. Anónimo a
    /// propósito — algunos productos son visibles en la búsqueda pública (app) sin sesión.
    /// </summary>
    [AllowAnonymous]
    [Route("api/v1/files")]
    public class FilesController : ControllerBase
    {
        private readonly IFileStorageService _storage;
        public FilesController(IFileStorageService storage) => _storage = storage;

        [HttpGet("{**key}")]
        public async Task<IActionResult> Get(string key, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(key)) return NotFound();
            try
            {
                var (stream, contentType) = await _storage.DownloadAsync(key, ct);
                Response.Headers.CacheControl = "public, max-age=31536000, immutable";
                return File(stream, contentType);
            }
            catch (FileNotFoundException)
            {
                return NotFound();
            }
        }
    }
}
