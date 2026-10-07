namespace BusinessSearcher.Application.DTOs.Operations
{
    public record OperationsUserDto(
        Guid Id, string Name, string Email, string Role, string? AvatarUrl,
        Guid? AssignedRegisterId, Guid? AssignedWarehouseId, bool IsActive);

    public record CreateOperationsUserDto(
        string Name, string Email, string Password, string Role,
        string? AvatarUrl = null, Guid? AssignedRegisterId = null, Guid? AssignedWarehouseId = null);

    public record UpdateOperationsUserDto(
        string Name, string Role, string? AvatarUrl = null,
        Guid? AssignedRegisterId = null, Guid? AssignedWarehouseId = null, bool IsActive = true,
        string? NewPassword = null);

    // ── Login de sub-usuario ──
    public record OpsLoginDto(string Email, string Password);

    public record OpsLoginResultDto(string Token, OperationsUserDto User, string BusinessName);
}
