using BusinessSearcher.API.Controllers;
using BusinessSearcher.Application.Commons.Interfaces;
using BusinessSearcher.Application.DTOs.Radar;
using BusinessSearcher.Application.Features.Radar.Commands.ClientAuth;
using BusinessSearcher.Application.Features.Radar.Commands.SetClientPlan;
using BusinessSearcher.Application.Features.Radar.Queries;
using BusinessSearcher.Application.Features.Radar.Queries.ClientProfile;
using BusinessSearcher.Domain.Identity;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BusinessSearcher.API.Controllers.v1
{
    /// <summary>
    /// Autenticación de Clientes consumidores (app móvil). Distinta de /api/v1/auth,
    /// que es para Tiendas (mayoristas/minoristas) en la web.
    /// </summary>
    [Route("api/v1/client/auth")]
    public class ClientAuthController : BaseApiController
    {
        private ICurrentUserService _currentUser =>
            HttpContext.RequestServices.GetRequiredService<ICurrentUserService>();

        /// <summary>
        /// Registra un nuevo cliente (solo desde la app). La cuenta queda pendiente de
        /// aprobación por un admin: no se entrega token ni cookie de sesión hasta que
        /// el cliente inicie sesión (Login) una vez aprobado.
        /// </summary>
        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<IActionResult> Register([FromBody] RegisterClientDto dto, CancellationToken ct)
        {
            var result = await Mediator.Send(new RegisterClientCommand(
                dto.FullName, dto.Email, dto.Password, GetPlatform(), GetClientIp(), dto.ReferralCode, dto.PhoneNumber), ct);

            if (!string.IsNullOrEmpty(result.RefreshToken))
                SetRefreshTokenCookie(result.RefreshToken);

            var message = string.IsNullOrEmpty(result.Token)
                ? "Registro exitoso. Tu cuenta está pendiente de aprobación por un administrador. Te notificaremos por email cuando puedas iniciar sesión."
                : "Registro exitoso.";
            return StatusCode(201, new { success = true, message, data = result with { RefreshToken = null } });
        }

        /// <summary>Login de cliente (solo desde la app).</summary>
        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginClientDto dto, CancellationToken ct)
        {
            var result = await Mediator.Send(new LoginClientCommand(
                dto.Email, dto.Password, GetPlatform(), GetClientIp()), ct);
            SetRefreshTokenCookie(result.RefreshToken!);
            return Ok(result with { RefreshToken = null }, "Login exitoso.");
        }

        /// <summary>Renueva el JWT usando el refresh token (cookie HttpOnly).</summary>
        [HttpPost("refresh-token")]
        [AllowAnonymous]
        public async Task<IActionResult> RefreshToken(CancellationToken ct)
        {
            var refreshToken = Request.Cookies["refreshToken"];
            if (string.IsNullOrEmpty(refreshToken))
                return Unauthorized(new { success = false, message = "Refresh token no encontrado." });

            var result = await Mediator.Send(new RefreshClientTokenCommand(refreshToken, GetClientIp()), ct);
            SetRefreshTokenCookie(result.RefreshToken!);
            return Ok(result with { RefreshToken = null });
        }

        /// <summary>Cierra la sesión actual (revoca el refresh token).</summary>
        [HttpPost("logout")]
        [Authorize(Roles = "Client")]
        public async Task<IActionResult> Logout(CancellationToken ct)
        {
            var refreshToken = Request.Cookies["refreshToken"];
            if (!string.IsNullOrEmpty(refreshToken))
            {
                await Mediator.Send(new LogoutClientCommand(_currentUser.AccountId, refreshToken), ct);
                Response.Cookies.Delete("refreshToken");
            }
            return Ok<object?>(null, "Sesión cerrada exitosamente.");
        }

        /// <summary>Perfil del cliente autenticado.</summary>
        [HttpGet("profile")]
        [Authorize(Roles = "Client")]
        public async Task<IActionResult> GetProfile(CancellationToken ct)
        {
            var result = await Mediator.Send(new GetClientProfileQuery(), ct);
            return Ok(result);
        }

        /// <summary>Registra el token FCM del dispositivo para notificaciones push (radar).</summary>
        [HttpPatch("fcm-token")]
        [Authorize(Roles = "Client")]
        public async Task<IActionResult> RegisterFcmToken([FromBody] ClientFcmTokenDto dto, CancellationToken ct)
        {
            await Mediator.Send(new RegisterClientFcmTokenCommand(_currentUser.AccountId, dto.Token), ct);
            return Ok<object?>(null, "Token FCM registrado exitosamente.");
        }

        /// <summary>
        /// El cliente pide pasar a Premium desde la app. Queda pendiente de revisión de
        /// un admin (no concede el plan directamente).
        /// </summary>
        [HttpPost("request-premium")]
        [Authorize(Roles = "Client")]
        public async Task<IActionResult> RequestPremium(CancellationToken ct)
        {
            var result = await Mediator.Send(new RequestPremiumCommand(_currentUser.AccountId), ct);
            return Ok(result, "Solicitud enviada. Un administrador la revisará pronto.");
        }

        /// <summary>Reporta un pago manual para pasar a Premium: teléfono, monto y comprobante. Queda pendiente hasta que un admin lo revise.</summary>
        [HttpPost("premium-claims")]
        [Authorize(Roles = "Client")]
        public async Task<IActionResult> SubmitPremiumClaim(
            [FromBody] SubmitPremiumClaimDto dto, CancellationToken ct)
        {
            var id = await Mediator.Send(new SubmitPremiumClaimCommand(
                _currentUser.AccountId, dto.PhoneNumber, dto.Amount, dto.Currency, dto.ProofReference), ct);
            return StatusCode(201, new
            {
                success = true,
                message = "Reporte de pago enviado. Un admin lo revisará pronto.",
                data = new { id }
            });
        }

        /// <summary>Historial de mis reportes de pago de Premium (autoservicio)</summary>
        [HttpGet("premium-claims")]
        [Authorize(Roles = "Client")]
        public async Task<IActionResult> GetMyPremiumClaims(CancellationToken ct)
        {
            var result = await Mediator.Send(new GetMyPremiumClaimsQuery(), ct);
            return Ok(result);
        }

        /// <summary>Actualiza nombre, teléfono y dirección/ubicación del cliente autenticado (autoservicio, desde el perfil).</summary>
        [HttpPatch("profile")]
        [Authorize(Roles = "Client")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateClientProfileDto dto, CancellationToken ct)
        {
            var result = await Mediator.Send(new UpdateClientProfileCommand(
                dto.Street, dto.City, dto.State, dto.Country, dto.Latitude, dto.Longitude,
                dto.FullName, dto.PhoneNumber), ct);
            return Ok(result, "Perfil actualizado exitosamente.");
        }

        /// <summary>
        /// Perfil público de un cliente-vendedor (nombre/teléfono/dirección) para que otro
        /// cliente vea dónde reside al tocar su producto en el buscador.
        /// </summary>
        [HttpGet("~/api/v1/client/{id:guid}/public-profile")]
        [Authorize(Roles = "Client")]
        public async Task<IActionResult> GetPublicProfile(Guid id, CancellationToken ct)
        {
            var result = await Mediator.Send(new GetClientPublicProfileQuery(id), ct);
            return Ok(result);
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        /// <summary>Plataforma de origen leída del header X-Client-Platform (por defecto Mobile).</summary>
        private ClientPlatform GetPlatform()
        {
            var raw = Request.Headers["X-Client-Platform"].FirstOrDefault()?.Trim().ToLowerInvariant();
            return raw is "web" or "browser" ? ClientPlatform.Web : ClientPlatform.Mobile;
        }

        private void SetRefreshTokenCookie(string token)
        {
            Response.Cookies.Append("refreshToken", token, new CookieOptions
            {
                HttpOnly = true,
                Secure   = true,
                // None: los clientes web viven en otro dominio (github.io) que la API
                SameSite = SameSiteMode.None,
                Expires  = DateTimeOffset.UtcNow.AddDays(30)
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
}
