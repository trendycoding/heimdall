using Heimdall.Application.Common.Interfaces;
using MediatR;

namespace Heimdall.Application.AccessDetails.Commands.UpdateUserAccessDetail;

/// <summary>
/// Updates a user access detail record. Invalidates the user's access detail cache.
/// The handler populates TenantId, ApplicationId, and UserProfileId from the loaded entity.
/// </summary>
public sealed class UpdateUserAccessDetailCommand : IRequest<Unit>, ICacheInvalidatingCommand
{
    public Guid Id { get; init; }
    public string AccessDetailValue { get; init; } = string.Empty;
    public string? Description { get; init; }
    public DateTime? ValidFrom { get; init; }
    public DateTime? ValidTo { get; init; }

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
