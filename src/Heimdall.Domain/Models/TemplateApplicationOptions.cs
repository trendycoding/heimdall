namespace Heimdall.Domain.Models;

public sealed class TemplateApplicationOptions
{
    public bool ReplaceExistingPermissions { get; init; }
    public bool ReplaceExistingGroups { get; init; }
    public bool ReplaceExistingAccessDetails { get; init; }
}
