using Heimdall.Application.Common.Interfaces;
using MediatR;

namespace Heimdall.Application.PermissionAssignments.Commands.DeleteGroupPermissionAssignment;

/// <summary>
/// Deletes a group permission assignment. Invalidates all group members' permission cache.
/// The handler populates TenantId and ApplicationId from the loaded entity before cache invalidation runs.
/// </summary>
public sealed class DeleteGroupPermissionAssignmentCommand : IRequest<Unit>, ICacheInvalidatingCommand
{
    public Guid Id { get; init; }

    // Populated by handler for cache invalidation
    internal Guid TenantId { get; set; }
    internal Guid ApplicationId { get; set; }

    // ICacheInvalidatingCommand — invalidate all permission caches for the application
    public IReadOnlyList<string> GetCacheKeyPrefixes()
    {
        if (TenantId == Guid.Empty)
            return [];

        return [$"perm:{TenantId}:{ApplicationId}:", $"perm-effective:{TenantId}:{ApplicationId}:"];
    }
}
