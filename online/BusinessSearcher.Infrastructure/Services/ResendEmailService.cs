using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace BusinessSearcher.Infrastructure.Services
{
    /// <summary>
    /// Envía emails por la API HTTP de Resend (https://resend.com), que va por HTTPS
    /// (puerto 443). Sirve en hosts como Render free, donde los puertos SMTP salientes
    /// están bloqueados. Reutiliza las plantillas HTML de <see cref="SmtpEmailService"/>
    /// y solo reemplaza el transporte.
    /// Config: Email:ResendApiKey, Email:FromEmail (dominio verificado), Email:FromName.
    /// </summary>
    public class ResendEmailService : SmtpEmailService
    {
        private readonly HttpClient _http;

        public ResendEmailService(IConfiguration config, ILogger<ResendEmailService> logger, HttpClient http)
            : base(config, (ILogger)logger)
        {
            _http = http;
        }

        protected override async Task SendEmailAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken)
        {
            try
            {
                var apiKey = Clean(_config["Email:ResendApiKey"]);
                if (apiKey.Length == 0)
                {
                    _logger.LogWarning("Email:ResendApiKey no configurado; email a {To} omitido.", to);
                    return;
                }

                var fromEmail = Clean(_config["Email:FromEmail"]);
                if (fromEmail.Length == 0) fromEmail = "onboarding@resend.dev"; // remitente de pruebas de Resend
                var fromName  = Clean(_config["Email:FromName"]) is { Length: > 0 } n ? n : "FoodFinder";

                var payload = new
                {
                    from    = $"{fromName} <{fromEmail}>",
                    to      = new[] { to },
                    subject = subject,
                    html    = htmlBody
                };

                using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails")
                {
                    Content = JsonContent.Create(payload)
                };
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(15));

                var resp = await _http.SendAsync(req, cts.Token);
                if (resp.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Email (Resend) enviado a {To} — Asunto: {Subject}", to, subject);
                }
                else
                {
                    var err = await resp.Content.ReadAsStringAsync(cts.Token);
                    _logger.LogError("Resend falló ({Status}) enviando a {To}: {Err}", (int)resp.StatusCode, to, err);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enviando email (Resend) a {To} — Asunto: {Subject}", to, subject);
            }
        }
    }
}
