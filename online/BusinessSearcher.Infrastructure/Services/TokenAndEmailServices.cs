using BusinessSearcher.Application.Commons.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;

namespace BusinessSearcher.Infrastructure.Services
{
    // ── Secure Token Generator ────────────────────────────────────────────────────
    public class SecureTokenGenerator : ISecureTokenGenerator
    {
        public string Generate(int sizeInBytes = 64)
        {
            var bytes = RandomNumberGenerator.GetBytes(sizeInBytes);
            return Convert.ToBase64String(bytes)
                .Replace("+", "-")
                .Replace("/", "_")
                .Replace("=", "");
        }
    }

    // ── Email Service (SMTP) ──────────────────────────────────────────────────────
    // Construye las plantillas HTML y las envía por SMTP. ResendEmailService hereda
    // de esta clase y solo reemplaza el transporte (SendEmailAsync) por HTTP.
    public class SmtpEmailService : IEmailService
    {
        protected readonly IConfiguration _config;
        protected readonly ILogger _logger;

        public SmtpEmailService(IConfiguration config, ILogger<SmtpEmailService> logger)
        {
            _config = config;
            _logger = logger;
        }

        protected SmtpEmailService(IConfiguration config, ILogger logger)
        {
            _config = config;
            _logger = logger;
        }

        public async Task SendPasswordResetEmailAsync(
            string toEmail, string businessName, string resetLink,
            CancellationToken cancellationToken = default)
        {
            var subject = "Recuperación de contraseña — BusinessSearcher";
            var body = $@"
                <html><body style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
                  <h2 style='color: #2563EB;'>Recuperación de contraseña</h2>
                  <p>Hola <strong>{businessName}</strong>,</p>
                  <p>Recibimos una solicitud para restablecer la contraseña de tu cuenta.</p>
                  <p>Haz clic en el siguiente botón para continuar. Este enlace expirará en <strong>60 minutos</strong>.</p>
                  <div style='margin: 30px 0;'>
                    <a href='{resetLink}'
                       style='background-color: #2563EB; color: white; padding: 14px 28px;
                              text-decoration: none; border-radius: 6px; font-size: 16px;'>
                      Restablecer contraseña
                    </a>
                  </div>
                  <p style='color: #6B7280; font-size: 14px;'>
                    Si no solicitaste este cambio, puedes ignorar este email.
                    Tu contraseña no será modificada.
                  </p>
                  <hr style='border-color: #E5E7EB;'/>
                  <p style='color: #9CA3AF; font-size: 12px;'>BusinessSearcher — Plataforma para negocios de comida</p>
                </body></html>";

            await SendEmailAsync(toEmail, subject, body, cancellationToken);
        }

        public async Task SendWelcomeEmailAsync(
            string toEmail, string businessName,
            CancellationToken cancellationToken = default)
        {
            var subject = $"¡Bienvenido a BusinessSearcher, {businessName}!";
            var body = $@"
                <html><body style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
                  <h2 style='color: #2563EB;'>¡Bienvenido!</h2>
                  <p>Hola <strong>{businessName}</strong>,</p>
                  <p>Tu cuenta en <strong>BusinessSearcher</strong> ha sido creada exitosamente.</p>
                  <p>Ya puedes empezar a gestionar tu negocio: agrega tus productos, configura horarios
                     y permite que tus clientes te encuentren en tiempo real.</p>
                  <div style='margin: 30px 0;'>
                    <a href='{_config["Frontend:DashboardUrl"] ?? "https://app.businesssearcher.com"}'
                       style='background-color: #2563EB; color: white; padding: 14px 28px;
                              text-decoration: none; border-radius: 6px; font-size: 16px;'>
                      Ir al panel de control
                    </a>
                  </div>
                  <p style='color: #6B7280; font-size: 14px;'>
                    Tu período de prueba gratuita dura <strong>30 días</strong>. ¡Aprovéchalo al máximo!
                  </p>
                  <hr style='border-color: #E5E7EB;'/>
                  <p style='color: #9CA3AF; font-size: 12px;'>BusinessSearcher — Plataforma para negocios de comida</p>
                </body></html>";

            await SendEmailAsync(toEmail, subject, body, cancellationToken);
        }

        public async Task SendPasswordChangedNotificationAsync(
            string toEmail, string businessName,
            CancellationToken cancellationToken = default)
        {
            var subject = "Tu contraseña ha sido cambiada — BusinessSearcher";
            var body = $@"
                <html><body style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
                  <h2 style='color: #2563EB;'>Contraseña actualizada</h2>
                  <p>Hola <strong>{businessName}</strong>,</p>
                  <p>Tu contraseña fue cambiada exitosamente el <strong>{DateTime.UtcNow:dd/MM/yyyy HH:mm} UTC</strong>.</p>
                  <p style='color: #DC2626;'>
                    Si <strong>no realizaste este cambio</strong>, contacta a soporte inmediatamente
                    en <a href='mailto:soporte@businesssearcher.com'>soporte@businesssearcher.com</a>.
                  </p>
                  <hr style='border-color: #E5E7EB;'/>
                  <p style='color: #9CA3AF; font-size: 12px;'>BusinessSearcher — Plataforma para negocios de comida</p>
                </body></html>";

            await SendEmailAsync(toEmail, subject, body, cancellationToken);
        }

        public async Task SendEmailVerificationAsync(
            string toEmail, string businessName, string verificationLink,
            CancellationToken cancellationToken = default)
        {
            var subject = "Verifica tu email — BusinessSearcher";
            var body = $@"
                <html><body style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
                  <h2 style='color: #2563EB;'>Verifica tu dirección de email</h2>
                  <p>Hola <strong>{businessName}</strong>,</p>
                  <p>Gracias por registrarte en <strong>BusinessSearcher</strong>.</p>
                  <p>Para activar tu cuenta haz clic en el siguiente botón.
                     Este enlace expirará en <strong>24 horas</strong>.</p>
                  <div style='margin: 30px 0;'>
                    <a href='{verificationLink}'
                       style='background-color: #16A34A; color: white; padding: 14px 28px;
                              text-decoration: none; border-radius: 6px; font-size: 16px;'>
                      Verificar mi email
                    </a>
                  </div>
                  <p style='color: #6B7280; font-size: 14px;'>
                    Si no creaste esta cuenta, ignora este correo.
                  </p>
                  <hr style='border-color: #E5E7EB;'/>
                  <p style='color: #9CA3AF; font-size: 12px;'>BusinessSearcher — Plataforma para negocios</p>
                </body></html>";

            await SendEmailAsync(toEmail, subject, body, cancellationToken);
        }

        // Trim + quitar comillas: valores pegados en paneles (Render, etc.) suelen
        // arrastrar espacios o comillas que rompen el envío. Accesible por subclases.
        protected static string Clean(string? v) => (v ?? string.Empty).Trim().Trim('"', '\'');

        protected virtual async Task SendEmailAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken)
        {
            try
            {
                var smtpHost     = Clean(_config["Email:SmtpHost"]);
                if (smtpHost.Length == 0) throw new InvalidOperationException("Email:SmtpHost no configurado.");
                var smtpPort     = int.Parse(Clean(_config["Email:SmtpPort"]) is { Length: > 0 } p ? p : "587");
                var smtpUser     = Clean(_config["Email:SmtpUser"]);
                if (smtpUser.Length == 0) throw new InvalidOperationException("Email:SmtpUser no configurado.");
                var smtpPassword = _config["Email:SmtpPassword"]?.Trim()
                                   ?? throw new InvalidOperationException("Email:SmtpPassword no configurado.");
                var fromEmail    = Clean(_config["Email:FromEmail"]) is { Length: > 0 } f ? f : smtpUser;
                var fromName     = Clean(_config["Email:FromName"]) is { Length: > 0 } n ? n : "BusinessSearcher";

                if (!MailAddress.TryCreate(fromEmail, out _))
                {
                    _logger.LogWarning(
                        "Email:FromEmail inválido ('{FromEmail}'); usando Email:SmtpUser como remitente", fromEmail);
                    fromEmail = smtpUser;
                }

                using var client = new SmtpClient(smtpHost, smtpPort)
                {
                    Credentials    = new NetworkCredential(smtpUser, smtpPassword),
                    EnableSsl      = true,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    Timeout        = 10_000
                };

                var message = new MailMessage
                {
                    From       = new MailAddress(fromEmail, fromName),
                    Subject    = subject,
                    Body       = htmlBody,
                    IsBodyHtml = true
                };
                message.To.Add(to);

                // Token propio: si el cliente HTTP aborta la petición, el email en curso
                // no debe cancelarse a mitad de envío (era la causa de TaskCanceledException)
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(12));
                await client.SendMailAsync(message, cts.Token);
                _logger.LogInformation("Email enviado a {To} — Asunto: {Subject}", to, subject);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error enviando email a {To} — Asunto: {Subject} — From configurado: '{From}'",
                    to, subject, _config["Email:FromEmail"] ?? _config["Email:SmtpUser"]);
            }
        }
    }
}
