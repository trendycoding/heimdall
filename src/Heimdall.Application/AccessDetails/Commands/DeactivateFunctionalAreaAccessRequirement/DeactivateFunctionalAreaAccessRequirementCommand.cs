using Heimdall.Application.Common.Interfaces;
using MediatR;

namespace Heimdall.Application.AccessDetails.Commands.DeactivateFunctionalAreaAccessRequirement;

/// <summary>
/// Deactivates a functional area access requirement. Invalidates access detail cache for the application.
/// The handler populates TenantId and ApplicationId from the loaded entity.
/// </summary>
public sealed class DeactivateFunctionalAreaAccessRequirementCommand : IRequest<Unit>, ICacheInvalidatingCommand
{
    public Guid Id { get; init; }

    // Populated by handler for cache invalidation
    internal Guid TenantId { get; set; }
    internal Guid ApplicationId { get; set; }

    // ICacheInvalidatingCommand
    public IReadOnlyList<string> GetCacheKeyPrefixes()
    {
        if (TenantId == Guid.Empty)
            return [];

        return [$"access:{TenantId}:{ApplicationId}:"];
    }
}
