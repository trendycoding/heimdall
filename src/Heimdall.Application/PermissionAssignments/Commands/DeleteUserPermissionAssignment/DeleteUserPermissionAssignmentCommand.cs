using Heimdall.Application.Common.Interfaces;
using MediatR;

namespace Heimdall.Application.PermissionAssignments.Commands.DeleteUserPermissionAssignment;

/// <summary>
/// Deletes a user permission assignment. Invalidates the user's permission and effective permission cache.
/// The handler populates TenantId, ApplicationId, and UserProfileId from the loaded entity before cache invalidation runs.
/// </summary>
public sealed class DeleteUserPermissionAssignmentCommand : IRequest<Unit>, ICacheInvalidatingCommand
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

        return [$"perm:{TenantId}:{ApplicationId}:{UserProfileId}:", $"perm-effective:{TenantId}:{ApplicationId}:{UserProfileId}"];
    }
}
