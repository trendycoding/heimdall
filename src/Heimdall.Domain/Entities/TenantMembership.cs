using Heimdall.Domain.Enums;

namespace Heimdall.Domain.Entities;

/// <summary>
/// Links a user (by external subject ID) to a tenant with a specific role.
/// This is NOT tenant-scoped — it spans tenants so a user can discover which tenants they belong to.
/// </summary>
public class TenantMembership : BaseEntity
{
    public Guid TenantId { get; set; }
    public string ExternalSubjectId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public TenantRole Role { get; set; }
    public MembershipStatus Status { get; set; }
    public string? InvitedBy { get; set; }
    public DateTime? AcceptedAt { get; set; }
}
