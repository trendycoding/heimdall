namespace Heimdall.Domain.Entities;

public class Group : ApplicationScopedEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}
