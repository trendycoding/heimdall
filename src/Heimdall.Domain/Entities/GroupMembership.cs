namespace Heimdall.Domain.Entities;

public class GroupMembership : ApplicationScopedEntity
{
    public Guid GroupId { get; set; }
    public Guid UserProfileId { get; set; }
}
