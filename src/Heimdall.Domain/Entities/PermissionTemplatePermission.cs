using Heimdall.Domain.Enums;

namespace Heimdall.Domain.Entities;

public class PermissionTemplatePermission : BaseEntity
{
    public Guid PermissionTemplateId { get; set; }
    public Guid PermissionId { get; set; }
    public Effect Effect { get; set; }
    public int? ValidFromOffsetDays { get; set; }
    public int? ValidToOffsetDays { get; set; }
}
