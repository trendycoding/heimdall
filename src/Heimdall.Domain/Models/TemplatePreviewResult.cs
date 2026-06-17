namespace Heimdall.Domain.Models;

public sealed class TemplatePreviewResult
{
    public IReadOnlyList<ProjectedPermissionChange> PermissionChanges { get; init; } = [];
    public IReadOnlyList<ProjectedGroupChange> GroupChanges { get; init; } = [];
    public IReadOnlyList<ProjectedAccessDetailChange> AccessDetailChanges { get; init; } = [];
}

public sealed class ProjectedPermissionChange
{
    public Guid PermissionId { get; init; }
    public string PermissionCode { get; init; } = string.Empty;
    public string Effect { get; init; } = string.Empty;
    public string ChangeType { get; init; } = string.Empty;
}

public sealed class ProjectedGroupChange
{
    public Guid GroupId { get; init; }
    public string GroupName { get; init; } = string.Empty;
    public string ChangeType { get; init; } = string.Empty;
}

public sealed class ProjectedAccessDetailChange
{
    public string AccessDetailType { get; init; } = string.Empty;
    public string AccessDetailCode { get; init; } = string.Empty;
    public string AccessDetailValue { get; init; } = string.Empty;
    public string ChangeType { get; init; } = string.Empty;
}
