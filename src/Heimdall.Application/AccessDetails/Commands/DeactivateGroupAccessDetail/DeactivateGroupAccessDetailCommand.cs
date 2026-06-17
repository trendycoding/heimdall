using Heimdall.Application.Common.Interfaces;
using MediatR;

namespace Heimdall.Application.AccessDetails.Commands.DeactivateGroupAccessDetail;

/// <summary>
/// Deactivates a group access detail record. Invalidates access detail cache for all group members.
/// The handler populates TenantId and ApplicationId from the loaded entity.
/// </summary>
public sealed class DeactivateGroupAccessDetailCommand : IRequest<Unit>, ICacheInvalidatingCommand
{
    public Guid Id { get; init; }

    // Populated by handler for cache invalidation
    internal Guid TenantId { get; set; }
    internal Guid ApplicationId { get; set; }

    // ICacheInvalidatingCommand — invalidate all access caches for the application
    public IReadOnlyList<string> GetCacheKeyPrefixes()
    {
        if (TenantId == Guid.Empty)
            return [];

        return [$"access:{TenantId}:{ApplicationId}:"];
    }
}
