namespace Heimdall.Domain.Models;

public sealed class TemplateApplicationResult
{
    public bool Success { get; init; }
    public int PermissionsApplied { get; init; }
    public int GroupMembershipsApplied { get; init; }
    public int AccessDetailsApplied { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];
}
