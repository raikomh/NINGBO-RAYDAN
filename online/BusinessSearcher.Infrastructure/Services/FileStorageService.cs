using BusinessSearcher.Application.Commons.Interfaces;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BusinessSearcher.Infrastructure.Services
{
    /// <summary>
    /// Servicio de almacenamiento de imágenes usando Cloudinary.
    /// Para el MVP es la mejor opción: gratuito hasta 25GB, CDN global incluido,
    /// transformaciones automáticas de imágenes, sin gestión de servidores.
    /// </summary>
    public class CloudinaryFileStorageService : IFileStorageService
    {
        private readonly Cloudinary _cloudinary;
        private readonly ILogger<CloudinaryFileStorageService> _logger;

        private static readonly string[] AllowedImageTypes =
            { "image/jpeg", "image/jpg", "image/png", "image/webp" };

        public CloudinaryFileStorageService(IConfiguration config, ILogger<CloudinaryFileStorageService> logger)
        {
            _logger = logger;

            var cloudName = config["Cloudinary:CloudName"] ?? throw new InvalidOperationException("Cloudinary:CloudName no configurado.");
            var apiKey    = config["Cloudinary:ApiKey"]    ?? throw new InvalidOperationException("Cloudinary:ApiKey no configurado.");
            var apiSecret = config["Cloudinary:ApiSecret"] ?? throw new InvalidOperationException("Cloudinary:ApiSecret no configurado.");

            var account   = new Account(cloudName, apiKey, apiSecret);
            _cloudinary   = new Cloudinary(account) { Api = { Secure = true } };
        }

        public async Task<string> UploadAsync(
            Stream fileStream, string fileName, string contentType,
            string folder, CancellationToken cancellationToken = default)
        {
            if (!IsValidImageContentType(contentType))
                throw new ArgumentException($"Tipo de archivo no permitido: {contentType}. Solo se aceptan: JPEG, PNG, WEBP.");

            // Genera un nombre de archivo único para evitar colisiones
            var publicId = $"{folder}/{Path.GetFileNameWithoutExtension(fileName)}_{Guid.NewGuid():N}";

            var uploadParams = new ImageUploadParams
            {
                File           = new FileDescription(fileName, fileStream),
                PublicId       = publicId,
                Folder         = $"business_searcher/{folder}",
                Transformation = new Transformation()
                    .Quality("auto")       // Calidad automática (menor tamaño)
                    .FetchFormat("auto"),  // Formato óptimo (webp en navegadores modernos)
                Overwrite      = false
            };

            var result = await _cloudinary.UploadAsync(uploadParams, cancellationToken);

            if (result.Error is not null)
            {
                _logger.LogError("Error Cloudinary al subir {FileName}: {Error}", fileName, result.Error.Message);
                throw new InvalidOperationException($"Error al subir imagen: {result.Error.Message}");
            }

            _logger.LogInformation("Imagen subida exitosamente: {Url}", result.SecureUrl);
            return result.SecureUrl.ToString();
        }

        public async Task DeleteAsync(string fileUrl, CancellationToken cancellationToken = default)
        {
            try
            {
                // Extrae el publicId desde la URL de Cloudinary
                var uri      = new Uri(fileUrl);
                var segments = uri.AbsolutePath.Split('/');
                var uploadIndex = Array.IndexOf(segments, "upload");
                if (uploadIndex < 0) return;

                // Toma todo después de /upload/v{version}/ como publicId (sin extensión)
                var publicId = string.Join("/",
                    segments.Skip(uploadIndex + 2))
                    .Replace(Path.GetExtension(fileUrl), "");

                var deleteParams = new DeletionParams(publicId);
                var result       = await _cloudinary.DestroyAsync(deleteParams);

                if (result.Result != "ok")
                    _logger.LogWarning("Cloudinary: no se pudo eliminar {PublicId} — {Result}", publicId, result.Result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error eliminando imagen de Cloudinary: {Url}", fileUrl);
            }
        }

        public bool IsValidImageContentType(string contentType) =>
            AllowedImageTypes.Contains(contentType.ToLowerInvariant());

        // Cloudinary ya devuelve URLs públicas directas (CDN propio): no necesita proxy.
        public Task<(Stream Stream, string ContentType)> DownloadAsync(string key, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("CloudinaryFileStorageService no implementa DownloadAsync: sus URLs ya son públicas.");
    }
}
