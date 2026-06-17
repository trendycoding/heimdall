namespace Heimdall.Domain.Models;

public sealed class AccessDetailLookupResult
{
    public IReadOnlyList<AccessDetailEntry> Details { get; init; } = [];
}

public sealed class AccessDetailEntry
{
    public Guid AccessDetailId { get; init; }
    public string AccessDetailType { get; init; } = string.Empty;
    public string AccessDetailCode { get; init; } = string.Empty;
    public string AccessDetailValue { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string Source { get; init; } = string.Empty;
    public Guid? GroupId { get; init; }
    public string? GroupName { get; init; }
}
