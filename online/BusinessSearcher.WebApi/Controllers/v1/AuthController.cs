using BusinessSearcher.Application.Features.TenantManagement.Commands.ChangePassword;
using BusinessSearcher.Application.Features.TenantManagement.Commands.ForgotPassword;
using BusinessSearcher.Application.Features.TenantManagement.Commands.LoginTenant;
using BusinessSearcher.Application.Features.TenantManagement.Commands.RefreshToken;
using BusinessSearcher.Application.Features.TenantManagement.Commands.RegisterTenant;
using BusinessSearcher.Application.Features.TenantManagement.Commands.ResetPassword;
using BusinessSearcher.Application.Features.TenantManagement.Commands.UpdateProfile;
using BusinessSearcher.Application.Features.TenantManagement.Commands.RegisterFcmToken;
using BusinessSearcher.Application.Features.TenantManagement.Commands;
using BusinessSearcher.Application.Features.TenantManagement.Queries;
using BusinessSearcher.Application.Features.StoreManagement.Queries;
using BusinessSearcher.Application.Commons.Interfaces;
using BusinessSearcher.Application.DTOs.TenantManagement;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using BusinessSearcher.API.Controllers;

namespace BusinessSearcher.API.Controllers.v1
{
    [Route("api/v1/auth")]
    public class AuthController : BaseApiController
    {
        private ICurrentUserService _currentUser =>
            HttpContext.RequestServices.GetRequiredService<ICurrentUserService>();

        /// <summary>Registra un nuevo negocio (tenant). Queda pendiente de verificación de email y de aprobación por un administrador antes de poder iniciar sesión.</summary>
        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<IActionResult> Register(
            [FromBody] RegisterTenantCommand command, CancellationToken ct)
        {
            var result = await Mediator.Send(command, ct);
            return StatusCode(201, new
            {
                success = true,
                message = "Registro exitoso. Verifica tu email y espera la aprobación de un administrador para poder iniciar sesión.",
                data = result
            });
        }

        /// <summary>Login con email y contraseña</summary>
        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login(
            [FromBody] LoginTenantCommand command, CancellationToken ct)
        {
            var platform  = GetPlatform();
            var ipCommand = command with { IpAddress = GetClientIp(), Platform = platform };
            var result    = await Mediator.Send(ipCommand, ct);
            SetRefreshTokenCookie(result.RefreshToken!);
            // Web: el refresh token viaja solo por cookie httpOnly (protección XSS), nunca en el body.
            // Mobile: no hay cookie jar compartida con el cliente nativo, así que va también en el body
            // para que la app lo guarde en SecureStore.
            var body = platform == BusinessSearcher.Domain.Identity.ClientPlatform.Mobile
                ? result
                : result with { RefreshToken = null };
            return Ok(body, "Login exitoso.");
        }

        /// <summary>Renueva el JWT usando el refresh token (cookie HttpOnly) — clientes web</summary>
        [HttpPost("refresh-token")]
        [AllowAnonymous]
        public async Task<IActionResult> RefreshToken(CancellationToken ct)
        {
            var refreshToken = Request.Cookies["refreshToken"];
            if (string.IsNullOrEmpty(refreshToken))
                return Unauthorized(new { success = false, message = "Refresh token no encontrado." });

            var result = await Mediator.Send(new RefreshTokenCommand(refreshToken, GetClientIp()), ct);
            SetRefreshTokenCookie(result.RefreshToken!);
            return Ok(result with { RefreshToken = null });
        }

        /// <summary>
        /// Renueva el JWT usando el refresh token recibido en el body — clientes móviles (sin cookie jar).
        /// Misma lógica de rotación que <see cref="RefreshToken"/>; el nuevo refresh token se devuelve en
        /// el body para que la app lo persista (SecureStore) en vez de una cookie httpOnly.
        /// </summary>
        [HttpPost("refresh-token-mobile")]
        [AllowAnonymous]
        public async Task<IActionResult> RefreshTokenMobile(
            [FromBody] RefreshTokenMobileRequest request, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(request?.RefreshToken))
                return Unauthorized(new { success = false, message = "Refresh token no encontrado." });

            var result = await Mediator.Send(new RefreshTokenCommand(request.RefreshToken, GetClientIp()), ct);
            return Ok(result, "Token renovado.");
        }

        /// <summary>Cierra la sesión actual (revoca el refresh token — cookie en web, body en mobile)</summary>
        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout(
            [FromBody] RefreshTokenMobileRequest? request, CancellationToken ct)
        {
            var refreshToken = Request.Cookies["refreshToken"] ?? request?.RefreshToken;
            if (!string.IsNullOrEmpty(refreshToken))
            {
                await Mediator.Send(new LogoutCommand(_currentUser.TenantId, refreshToken), ct);
                Response.Cookies.Delete("refreshToken");
            }
            return Ok<object?>(null, "Sesión cerrada exitosamente.");
        }

        /// <summary>Cierra todas las sesiones en todos los dispositivos</summary>
        [HttpPost("logout-all")]
        [Authorize]
        public async Task<IActionResult> LogoutAll(CancellationToken ct)
        {
            await Mediator.Send(new LogoutAllSessionsCommand(_currentUser.TenantId), ct);
            Response.Cookies.Delete("refreshToken");
            return Ok<object?>(null, "Todas las sesiones fueron cerradas.");
        }

        /// <summary>Solicita un email de recuperación de contraseña</summary>
        [HttpPost("forgot-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ForgotPassword(
            [FromBody] ForgotPasswordCommand command, CancellationToken ct)
        {
            await Mediator.Send(command, ct);
            // Siempre retorna 200 (nunca revela si el email existe)
            return Ok<object?>(null, "Si tu email está registrado, recibirás un enlace de recuperación en los próximos minutos.");
        }

        /// <summary>Restablece la contraseña usando el token del email</summary>
        [HttpPost("reset-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ResetPassword(
            [FromBody] ResetPasswordCommand command, CancellationToken ct)
        {
            await Mediator.Send(command, ct);
            return Ok<object?>(null, "Contraseña restablecida exitosamente. Ya puedes iniciar sesión.");
        }

        /// <summary>Cambia la contraseña (usuario autenticado)</summary>
        [HttpPost("change-password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword(
            [FromBody] ChangePasswordCommand command, CancellationToken ct)
        {
            await Mediator.Send(command, ct);
            Response.Cookies.Delete("refreshToken");
            return Ok<object?>(null, "Contraseña cambiada exitosamente. Por seguridad, tus otras sesiones fueron cerradas.");
        }

        /// <summary>Obtiene el perfil del tenant autenticado</summary>
        [HttpGet("profile")]
        [Authorize]
        public async Task<IActionResult> GetProfile(CancellationToken ct)
        {
            var result = await Mediator.Send(new GetTenantProfileQuery(), ct);
            return Ok(result);
        }

        /// <summary>Verifica el email usando el token enviado al correo</summary>
        [HttpPost("verify-email")]
        [AllowAnonymous]
        public async Task<IActionResult> VerifyEmail([FromQuery] string token, CancellationToken ct)
        {
            await Mediator.Send(new VerifyEmailCommand(token), ct);
            return Ok<object?>(null, "Email verificado exitosamente. Ya puedes iniciar sesión.");
        }

        /// <summary>Reenvía el email de verificación (por email, sin necesidad de sesión)</summary>
        [HttpPost("resend-verification")]
        [AllowAnonymous]
        public async Task<IActionResult> ResendVerification(
            [FromBody] ResendVerificationEmailCommand command, CancellationToken ct)
        {
            await Mediator.Send(command, ct);
            return Ok<object?>(null, "Si tu cuenta existe y no ha sido verificada, recibirás un nuevo enlace en los próximos minutos.");
        }

        /// <summary>Actualiza el nombre del negocio del tenant autenticado</summary>
        [HttpPatch("profile")]
        [Authorize]
        public async Task<IActionResult> UpdateProfile(
            [FromBody] UpdateTenantDto dto, CancellationToken ct)
        {
            await Mediator.Send(new UpdateTenantProfileCommand(dto.BusinessName), ct);
            return Ok<object?>(null, "Perfil actualizado exitosamente.");
        }

        /// <summary>Registra el token FCM del dispositivo para notificaciones push</summary>
        [HttpPatch("fcm-token")]
        [Authorize]
        public async Task<IActionResult> RegisterFcmToken(
            [FromBody] FcmTokenDto dto, CancellationToken ct)
        {
            await Mediator.Send(new RegisterFcmTokenCommand(dto.Token), ct);
            return Ok<object?>(null, "Token FCM registrado exitosamente.");
        }

        /// <summary>Reporta un pago manual (autoservicio): teléfono, monto y comprobante. Queda pendiente hasta que un admin lo revise.</summary>
        [HttpPost("payment-claims")]
        [Authorize]
        public async Task<IActionResult> SubmitPaymentClaim(
            [FromBody] SubmitPaymentClaimCommand command, CancellationToken ct)
        {
            var id = await Mediator.Send(command, ct);
            return StatusCode(201, new
            {
                success = true,
                message = "Reporte de pago enviado. Un admin lo revisará pronto.",
                data = new { id }
            });
        }

        /// <summary>Historial de mis reportes de pago (autoservicio)</summary>
        [HttpGet("payment-claims")]
        [Authorize]
        public async Task<IActionResult> GetMyPaymentClaims(CancellationToken ct)
        {
            var result = await Mediator.Send(new GetMyPaymentClaimsQuery(), ct);
            return Ok(result);
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        /// <summary>Plataforma de origen (header X-Client-Platform). Las tiendas son web por defecto.</summary>
        private BusinessSearcher.Domain.Identity.ClientPlatform GetPlatform()
        {
            var raw = Request.Headers["X-Client-Platform"].FirstOrDefault()?.Trim().ToLowerInvariant();
            return raw is "mobile" or "app"
                ? BusinessSearcher.Domain.Identity.ClientPlatform.Mobile
                : BusinessSearcher.Domain.Identity.ClientPlatform.Web;
        }

        private void SetRefreshTokenCookie(string token)
        {
            Response.Cookies.Append("refreshToken", token, new CookieOptions
            {
                HttpOnly  = true,   // No accesible desde JS (protección XSS)
                Secure    = true,   // Solo HTTPS
                // None: el frontend (github.io) y la API (onrender.com) son dominios
                // distintos; con Strict el navegador nunca enviaría la cookie al refresh
                SameSite  = SameSiteMode.None,
                Expires   = DateTimeOffset.UtcNow.AddDays(30)
            });
        }

        private string GetClientIp()
        {
            var forwarded = Request.Headers["X-Forwarded-For"].FirstOrDefault();
            return forwarded?.Split(',')[0].Trim()
                ?? HttpContext.Connection.RemoteIpAddress?.ToString()
                ?? "unknown";
        }
    }

    /// <summary>Body de las llamadas de auth móviles que necesitan el refresh token (sin cookie jar).</summary>
    public record RefreshTokenMobileRequest(string RefreshToken);
}
