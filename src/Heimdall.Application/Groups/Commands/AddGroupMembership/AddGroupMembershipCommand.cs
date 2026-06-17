using Heimdall.Application.Common.Interfaces;
using MediatR;

namespace Heimdall.Application.Groups.Commands.AddGroupMembership;

/// <summary>
/// Adds a user to a group. Invalidates user's permission, effective, access, and groups cache.
/// </summary>
public sealed class AddGroupMembershipCommand : IRequest<AddGroupMembershipResult>, ICacheInvalidatingCommand
{
    public Guid TenantId { get; init; }
    public Guid ApplicationId { get; init; }
    public Guid GroupId { get; init; }
    public Guid UserProfileId { get; init; }

    // ICacheInvalidatingCommand
    public IReadOnlyList<string> GetCacheKeyPrefixes()
        =>
        [
            $"perm:{TenantId}:{ApplicationId}:{UserProfileId}:",
            $"perm-effective:{TenantId}:{ApplicationId}:{UserProfileId}",
            $"access:{TenantId}:{ApplicationId}:{UserProfileId}:",
            $"groups:{TenantId}:{ApplicationId}:{UserProfileId}"
        ];
}

public sealed record AddGroupMembershipResult(Guid Id);
