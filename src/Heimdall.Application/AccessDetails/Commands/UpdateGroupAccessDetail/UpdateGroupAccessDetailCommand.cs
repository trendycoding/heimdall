using Heimdall.Application.Common.Interfaces;
using MediatR;

namespace Heimdall.Application.AccessDetails.Commands.UpdateGroupAccessDetail;

/// <summary>
/// Updates a group access detail record. Invalidates access detail cache for all group members.
/// The handler populates TenantId and ApplicationId from the loaded entity.
/// </summary>
public sealed class UpdateGroupAccessDetailCommand : IRequest<Unit>, ICacheInvalidatingCommand
{
    public Guid Id { get; init; }
    public string AccessDetailValue { get; init; } = string.Empty;
    public string? Description { get; init; }
    public DateTime? ValidFrom { get; init; }
    public DateTime? ValidTo { get; init; }

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
