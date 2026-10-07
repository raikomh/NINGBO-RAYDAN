namespace BusinessSearcher.Application.DTOs.Operations
{
    // ── Datos del negocio ──
    public record BusinessInfoDto(Guid Id, string Name, string? Address, string? Phone, string? Email, string? TaxId, string? LogoUrl);
    public record SaveBusinessInfoDto(string Name, string? Address = null, string? Phone = null, string? Email = null, string? TaxId = null, string? LogoUrl = null);

    // ── Auditoría ──
    public record AuditLogDto(
        Guid Id, Guid? UserId, string? UserName, string? UserRole, string Action, string? TargetEntity,
        string? Details, DateTime Timestamp, string? IpAddress, string Status, string? Method, string? Path);

    // ── Configuración clave-valor ──
    public record SettingDto(string Key, string Value);
    public record SaveSettingDto(string Value);
}
