namespace Heimdall.Domain.Models;

public sealed class EffectivePermissionsResult
{
    public IReadOnlyList<EffectivePermission> Permissions { get; init; } = [];
}

public sealed class EffectivePermission
{
    public Guid PermissionId { get; init; }
    public string PermissionCode { get; init; } = string.Empty;
    public string FunctionalAreaCode { get; init; } = string.Empty;
    public string PermissionTypeCode { get; init; } = string.Empty;
    public string Effect { get; init; } = string.Empty;
}
