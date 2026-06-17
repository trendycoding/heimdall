namespace Heimdall.Domain.Models;

public sealed class PermissionCheckResult
{
    public bool Allowed { get; init; }
    public string Decision { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public IReadOnlyList<MatchedAssignment> MatchedAssignments { get; init; } = [];
}

public sealed class MatchedAssignment
{
    public Guid AssignmentId { get; init; }
    public string Source { get; init; } = string.Empty;
    public string Effect { get; init; } = string.Empty;
    public Guid? GroupId { get; init; }
    public string? GroupName { get; init; }
}
