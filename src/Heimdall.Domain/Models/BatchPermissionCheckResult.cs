namespace Heimdall.Domain.Models;

public sealed class BatchPermissionCheckResult
{
    public IReadOnlyList<PermissionCheckResult> Results { get; init; } = [];
}
