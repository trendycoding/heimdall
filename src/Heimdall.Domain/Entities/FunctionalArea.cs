namespace Heimdall.Domain.Entities;

public class FunctionalArea : ApplicationScopedEntity
{
    public string FunctionalAreaCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}
