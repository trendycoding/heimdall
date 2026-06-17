namespace Heimdall.Domain.Entities;

public class PermissionType : ApplicationScopedEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsSystemReserved { get; set; }
    public bool IsActive { get; set; } = true;
}
