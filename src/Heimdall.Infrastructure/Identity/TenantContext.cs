using Heimdall.Domain.Interfaces;

namespace Heimdall.Infrastructure.Identity;

public class TenantContext : ITenantContext
{
    public Guid TenantId { get; set; }
    public Guid? ApplicationId { get; set; }
    public string ActorSubjectId { get; set; } = string.Empty;
    public string ActorEmail { get; set; } = string.Empty;
    public Guid? ActorUserProfileId { get; set; }
    public IReadOnlyList<string> AdminScopes { get; set; } = Array.Empty<string>();
    public bool IsSuperAdmin { get; set; }
}
