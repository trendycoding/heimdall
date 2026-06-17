using Heimdall.Domain.Enums;

namespace Heimdall.Domain.Entities;

public class UserPermissionAssignment : ApplicationScopedEntity
{
    public Guid UserProfileId { get; set; }
    public Guid PermissionId { get; set; }
    public Effect Effect { get; set; }
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
}
