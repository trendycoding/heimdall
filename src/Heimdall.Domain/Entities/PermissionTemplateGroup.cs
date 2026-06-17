namespace Heimdall.Domain.Entities;

public class PermissionTemplateGroup : BaseEntity
{
    public Guid PermissionTemplateId { get; set; }
    public Guid GroupId { get; set; }
}
