using Heimdall.Domain.Enums;

namespace Heimdall.Domain.Entities;

public class UserProfile : TenantScopedEntity
{
    public string ExternalSubjectId { get; set; } = string.Empty;
    public string IdentityProvider { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public UserStatus Status { get; set; }
    public DateTime? LastLoginAt { get; set; }
}
