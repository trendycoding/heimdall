using Heimdall.Application.Common.Interfaces;
using MediatR;

namespace Heimdall.Application.FunctionalAreas.Commands.DeactivateFunctionalArea;

/// <summary>
/// Deactivates a functional area. Triggers broad invalidation of all permission and access caches for the application.
/// The handler populates TenantId and ApplicationId from the loaded entity.
/// </summary>
public sealed class DeactivateFunctionalAreaCommand : IRequest<Unit>, ICacheInvalidatingCommand
{
    public Guid Id { get; init; }

    // Populated by handler for cache invalidation
    internal Guid TenantId { get; set; }
    internal Guid ApplicationId { get; set; }

    // ICacheInvalidatingCommand — broad invalidation for the entire application
    public IReadOnlyList<string> GetCacheKeyPrefixes()
    {
        if (TenantId == Guid.Empty)
            return [];

        return
        [
            $"perm:{TenantId}:{ApplicationId}:",
            $"perm-effective:{TenantId}:{ApplicationId}:",
            $"access:{TenantId}:{ApplicationId}:"
        ];
    }
}
