using Heimdall.Application.Common.Interfaces;
using MediatR;

namespace Heimdall.Application.AccessDetails.Commands.DeactivateUserAccessDetail;

/// <summary>
/// Deactivates a user access detail record. Invalidates the user's access detail cache.
/// The handler populates TenantId, ApplicationId, and UserProfileId from the loaded entity.
/// </summary>
public sealed class DeactivateUserAccessDetailCommand : IRequest<Unit>, ICacheInvalidatingCommand
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

        return [$"access:{TenantId}:{ApplicationId}:{UserProfileId}:"];
    }
}
