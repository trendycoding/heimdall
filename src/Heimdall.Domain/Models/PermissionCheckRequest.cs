namespace Heimdall.Domain.Models;

public sealed class PermissionCheckRequest
{
    public string? FunctionalAreaCode { get; init; }
    public string? PermissionTypeCode { get; init; }
    public string? PermissionCode { get; init; }
}
