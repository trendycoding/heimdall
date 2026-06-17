using Heimdall.Application.Common.Interfaces;
using MediatR;

namespace Heimdall.Application.Groups.Commands.RemoveGroupMembership;

/// <summary>
/// Removes a user from a group. Invalidates user's permission, effective, access, and groups cache.
/// The handler populates TenantId, ApplicationId, and UserProfileId from the loaded entity.
/// </summary>
public sealed class RemoveGroupMembershipCommand : IRequest<Unit>, ICacheInvalidatingCommand
{
    public Guid Id { get; init; }

    // Populated by handler for cache invalidation
    internal Guid TenantId { get; set; }
    internal Guid ApplicationId { get; set; }
    internal Guid UserProfileId { get; set; }

    // ICacheInvalidatingCommand
    public IReadOnlyList<string> GetCacheKeyPrefixes()
    {
        if (TenantId == Guid.Empty)
            return [];

        return
        [
            $"perm:{TenantId}:{ApplicationId}:{UserProfileId}:",
            $"perm-effective:{TenantId}:{ApplicationId}:{UserProfileId}",
            $"access:{TenantId}:{ApplicationId}:{UserProfileId}:",
            $"groups:{TenantId}:{ApplicationId}:{UserProfileId}"
        ];
    }
}
