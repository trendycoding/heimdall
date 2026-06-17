using Heimdall.Application.Common.Interfaces;
using MediatR;

namespace Heimdall.Application.Groups.Commands.DeactivateGroup;

/// <summary>
/// Deactivates a group. Triggers broad invalidation of permission and access caches for the application,
/// since group deactivation affects all group members' permission resolution.
/// The handler populates TenantId and ApplicationId from the loaded entity.
/// </summary>
public sealed class DeactivateGroupCommand : IRequest<Unit>, ICacheInvalidatingCommand
{
    public Guid Id { get; init; }

    // Populated by handler for cache invalidation
    internal Guid TenantId { get; set; }
    internal Guid ApplicationId { get; set; }

    // ICacheInvalidatingCommand — broad invalidation because group status affects all members
    public IReadOnlyList<string> GetCacheKeyPrefixes()
    {
        if (TenantId == Guid.Empty)
            return [];

        return
        [
            $"perm:{TenantId}:{ApplicationId}:",
            $"perm-effective:{TenantId}:{ApplicationId}:",
            $"access:{TenantId}:{ApplicationId}:",
            $"groups:{TenantId}:{ApplicationId}:"
        ];
    }
}
