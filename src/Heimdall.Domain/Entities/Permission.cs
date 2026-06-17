namespace Heimdall.Domain.Entities;

public class Permission : ApplicationScopedEntity
{
    public Guid FunctionalAreaId { get; set; }
    public Guid PermissionTypeId { get; set; }
    public string PermissionCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}
