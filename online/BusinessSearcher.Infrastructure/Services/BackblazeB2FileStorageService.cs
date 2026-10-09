using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BusinessSearcher.Application.Commons.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BusinessSearcher.Infrastructure.Services
{
    /// <summary>
    /// Almacenamiento de imágenes en Backblaze B2 usando su API nativa (b2_authorize_account /
    /// b2_get_upload_url / subida con cabeceras X-Bz-*), no la compatible con S3: así no hace
    /// falta el SDK de AWS, solo HttpClient. El bucket debe estar configurado como "Public" en
    /// Backblaze (Bucket Settings) para que las URLs devueltas se vean directo en el navegador.
    /// </summary>
    public class BackblazeB2FileStorageService : IFileStorageService
    {
        private readonly HttpClient _http;
        private readonly string _keyId;
        private readonly string _applicationKey;
        private readonly string _bucketId;
        private readonly string _bucketName;
        private readonly ILogger<BackblazeB2FileStorageService> _logger;

        private static readonly string[] AllowedImageTypes =
            { "image/jpeg", "image/jpg", "image/png", "image/webp" };

        public BackblazeB2FileStorageService(
            IHttpClientFactory httpClientFactory, IConfiguration config, ILogger<BackblazeB2FileStorageService> logger)
        {
            _http   = httpClientFactory.CreateClient(nameof(BackblazeB2FileStorageService));
            _logger = logger;

            _keyId          = config["Backblaze:KeyId"]          ?? throw new InvalidOperationException("Backblaze:KeyId no configurado.");
            _applicationKey = config["Backblaze:ApplicationKey"] ?? throw new InvalidOperationException("Backblaze:ApplicationKey no configurado.");
            _bucketId       = config["Backblaze:BucketId"]       ?? throw new InvalidOperationException("Backblaze:BucketId no configurado.");
            _bucketName     = config["Backblaze:BucketName"]     ?? throw new InvalidOperationException("Backblaze:BucketName no configurado.");
        }

        private record AuthResult(string ApiUrl, string DownloadUrl, string AuthorizationToken);

        private async Task<AuthResult> AuthorizeAsync(CancellationToken ct)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "https://api.backblazeb2.com/b2api/v3/b2_authorize_account");
            var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_keyId}:{_applicationKey}"));
            req.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);

            using var res = await _http.SendAsync(req, ct);
            var body = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode)
                throw new InvalidOperationException($"Backblaze b2_authorize_account falló ({(int)res.StatusCode}): {body}");

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var apiInfo = root.GetProperty("apiInfo").GetProperty("storageApi");
            return new AuthResult(
                apiInfo.GetProperty("apiUrl").GetString()!,
                apiInfo.GetProperty("downloadUrl").GetString()!,
                root.GetProperty("authorizationToken").GetString()!);
        }

        public async Task<string> UploadAsync(
            Stream fileStream, string fileName, string contentType,
            string folder, CancellationToken cancellationToken = default)
        {
            if (!IsValidImageContentType(contentType))
                throw new ArgumentException($"Tipo de archivo no permitido: {contentType}. Solo se aceptan: JPEG, PNG, WEBP.");

            var key = $"{folder}/{Path.GetFileNameWithoutExtension(fileName)}_{Guid.NewGuid():N}{Path.GetExtension(fileName)}";

            using var ms = new MemoryStream();
            await fileStream.CopyToAsync(ms, cancellationToken);
            var bytes = ms.ToArray();
            var sha1Hex = Convert.ToHexString(SHA1.HashData(bytes)).ToLowerInvariant();

            try
            {
                var auth = await AuthorizeAsync(cancellationToken);

                using var uploadUrlReq = new HttpRequestMessage(HttpMethod.Post, $"{auth.ApiUrl}/b2api/v3/b2_get_upload_url");
                uploadUrlReq.Headers.Authorization = new AuthenticationHeaderValue(auth.AuthorizationToken);
                uploadUrlReq.Content = JsonContent(new { bucketId = _bucketId });
                using var uploadUrlRes = await _http.SendAsync(uploadUrlReq, cancellationToken);
                var uploadUrlBody = await uploadUrlRes.Content.ReadAsStringAsync(cancellationToken);
                if (!uploadUrlRes.IsSuccessStatusCode)
                    throw new InvalidOperationException($"Backblaze b2_get_upload_url falló ({(int)uploadUrlRes.StatusCode}): {uploadUrlBody}");

                using var uploadUrlDoc = JsonDocument.Parse(uploadUrlBody);
                var uploadUrl = uploadUrlDoc.RootElement.GetProperty("uploadUrl").GetString()!;
                var uploadAuthToken = uploadUrlDoc.RootElement.GetProperty("authorizationToken").GetString()!;

                using var uploadReq = new HttpRequestMessage(HttpMethod.Post, uploadUrl);
                uploadReq.Headers.Authorization = new AuthenticationHeaderValue(uploadAuthToken);
                uploadReq.Headers.Add("X-Bz-File-Name", Uri.EscapeDataString(key));
                uploadReq.Headers.Add("X-Bz-Content-Sha1", sha1Hex);
                var content = new ByteArrayContent(bytes);
                content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
                uploadReq.Content = content;

                using var uploadRes = await _http.SendAsync(uploadReq, cancellationToken);
                var uploadBody = await uploadRes.Content.ReadAsStringAsync(cancellationToken);
                if (!uploadRes.IsSuccessStatusCode)
                    throw new InvalidOperationException($"Backblaze: error al subir archivo ({(int)uploadRes.StatusCode}): {uploadBody}");

                var url = $"{auth.DownloadUrl}/file/{_bucketName}/{key}";
                _logger.LogInformation("Imagen subida exitosamente: {Url}", url);
                return url;
            }
            catch (Exception ex) when (ex is not InvalidOperationException && ex is not ArgumentException)
            {
                _logger.LogError(ex, "Error Backblaze B2 al subir {FileName}", fileName);
                throw new InvalidOperationException($"Error al subir imagen: {ex.Message}");
            }
        }

        public async Task DeleteAsync(string fileUrl, CancellationToken cancellationToken = default)
        {
            try
            {
                var marker = $"/file/{_bucketName}/";
                var idx = fileUrl.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
                if (idx < 0) return;
                var key = Uri.UnescapeDataString(fileUrl[(idx + marker.Length)..]);

                var auth = await AuthorizeAsync(cancellationToken);

                // b2_delete_file_version exige el fileId: se busca por nombre exacto primero.
                using var listReq = new HttpRequestMessage(HttpMethod.Post, $"{auth.ApiUrl}/b2api/v3/b2_list_file_names");
                listReq.Headers.Authorization = new AuthenticationHeaderValue(auth.AuthorizationToken);
                listReq.Content = JsonContent(new { bucketId = _bucketId, startFileName = key, maxFileCount = 1 });
                using var listRes = await _http.SendAsync(listReq, cancellationToken);
                var listBody = await listRes.Content.ReadAsStringAsync(cancellationToken);
                if (!listRes.IsSuccessStatusCode) { _logger.LogWarning("Backblaze b2_list_file_names falló: {Body}", listBody); return; }

                using var listDoc = JsonDocument.Parse(listBody);
                var files = listDoc.RootElement.GetProperty("files");
                if (files.GetArrayLength() == 0) return;
                var first = files[0];
                if (first.GetProperty("fileName").GetString() != key) return; // no es el mismo archivo
                var fileId = first.GetProperty("fileId").GetString();

                using var delReq = new HttpRequestMessage(HttpMethod.Post, $"{auth.ApiUrl}/b2api/v3/b2_delete_file_version");
                delReq.Headers.Authorization = new AuthenticationHeaderValue(auth.AuthorizationToken);
                delReq.Content = JsonContent(new { fileName = key, fileId });
                using var delRes = await _http.SendAsync(delReq, cancellationToken);
                if (!delRes.IsSuccessStatusCode)
                    _logger.LogWarning("Backblaze: no se pudo eliminar {Key} — {Body}", key, await delRes.Content.ReadAsStringAsync(cancellationToken));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error eliminando imagen de Backblaze B2: {Url}", fileUrl);
            }
        }

        public bool IsValidImageContentType(string contentType) =>
            AllowedImageTypes.Contains(contentType.ToLowerInvariant());

        private static StringContent JsonContent(object payload) =>
            new(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
    }
}
