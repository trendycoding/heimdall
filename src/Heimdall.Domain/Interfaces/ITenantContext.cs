namespace Heimdall.Domain.Interfaces;

public interface ITenantContext
{
    Guid TenantId { get; }
    Guid? ApplicationId { get; }
    string ActorSubjectId { get; }
    string ActorEmail { get; }
    Guid? ActorUserProfileId { get; }
    IReadOnlyList<string> AdminScopes { get; }
    bool IsSuperAdmin { get; }
}
