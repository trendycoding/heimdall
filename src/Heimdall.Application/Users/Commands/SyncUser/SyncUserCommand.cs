using Heimdall.Application.Common.Interfaces;
using MediatR;

namespace Heimdall.Application.Users.Commands.SyncUser;

/// <summary>
/// Upserts a UserProfile by (TenantId, ExternalSubjectId, IdentityProvider).
/// If the user exists, updates mutable fields. If not, creates with Status=Active.
/// Optionally applies permission templates when applications array is provided.
/// </summary>
public sealed class SyncUserCommand : IRequest<SyncUserResult>, IAuditableCommand, ICacheInvalidatingCommand
{
    public Guid TenantId { get; init; }
    public string ExternalSubjectId { get; init; } = string.Empty;
    public string IdentityProvider { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Optional list of applications with permission template codes to apply during sync.
    /// </summary>
    public IReadOnlyList<SyncUserApplicationEntry> Applications { get; init; } = [];

    // IAuditableCommand
    public string EntityType => "UserProfile";
    public Guid EntityId { get; internal set; }
    public string AuditAction => "Sync";

    // ICacheInvalidatingCommand
    public IReadOnlyList<string> GetCacheKeyPrefixes()
    {
        if (EntityId == Guid.Empty)
            return [];

        return [$"perm:{TenantId}:*:{EntityId}:*", $"perm-effective:{TenantId}:*:{EntityId}", $"groups:{TenantId}:*:{EntityId}"];
    }
}

public sealed class SyncUserApplicationEntry
{
    public Guid ApplicationId { get; init; }
    public IReadOnlyList<string> PermissionTemplateCodes { get; init; } = [];
}

public sealed class SyncUserResult
{
    public Guid UserProfileId { get; init; }
    public bool IsNewUser { get; init; }
    public int TemplatesApplied { get; init; }
}
