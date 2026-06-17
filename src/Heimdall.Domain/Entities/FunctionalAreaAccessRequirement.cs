namespace Heimdall.Domain.Entities;

public class FunctionalAreaAccessRequirement : ApplicationScopedEntity
{
    public Guid FunctionalAreaId { get; set; }
    public string AccessDetailType { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}
