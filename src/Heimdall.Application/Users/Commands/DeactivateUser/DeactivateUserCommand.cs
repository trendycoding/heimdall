using Heimdall.Application.Common.Interfaces;
using MediatR;

namespace Heimdall.Application.Users.Commands.DeactivateUser;

/// <summary>
/// Sets a UserProfile Status to Inactive (soft delete).
/// </summary>
public sealed class DeactivateUserCommand : IRequest<DeactivateUserResult>, IAuditableCommand, ICacheInvalidatingCommand
{
    public Guid TenantId { get; init; }
    public Guid UserProfileId { get; init; }

    // IAuditableCommand
    public string EntityType => "UserProfile";
    public Guid EntityId => UserProfileId;
    public string AuditAction => "Deactivate";

    // ICacheInvalidatingCommand
    public IReadOnlyList<string> GetCacheKeyPrefixes()
        => [$"perm:{TenantId}:*:{UserProfileId}:*", $"perm-effective:{TenantId}:*:{UserProfileId}", $"groups:{TenantId}:*:{UserProfileId}"];
}

public sealed class DeactivateUserResult
{
    public Guid UserProfileId { get; init; }
    public bool Success { get; init; }
}
