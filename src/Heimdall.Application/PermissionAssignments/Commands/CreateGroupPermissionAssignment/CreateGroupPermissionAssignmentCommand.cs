using Heimdall.Application.Common.Interfaces;
using Heimdall.Domain.Enums;
using Heimdall.Domain.Interfaces;
using MediatR;

namespace Heimdall.Application.PermissionAssignments.Commands.CreateGroupPermissionAssignment;

/// <summary>
/// Creates a group permission assignment. Invalidates all group members' permission cache.
/// Since we cannot enumerate members at the command level, uses application-wide prefix invalidation.
/// </summary>
public sealed class CreateGroupPermissionAssignmentCommand : IRequest<CreateGroupPermissionAssignmentResult>, ICacheInvalidatingCommand
{
    public Guid TenantId { get; init; }
    public Guid ApplicationId { get; init; }
    public Guid GroupId { get; init; }
    public Guid PermissionId { get; init; }
    public Effect Effect { get; init; }
    public DateTime? ValidFrom { get; init; }
    public DateTime? ValidTo { get; init; }

    // ICacheInvalidatingCommand — invalidate all permission caches for the application
    // because group permission changes affect all group members
    public IReadOnlyList<string> GetCacheKeyPrefixes()
        => [$"perm:{TenantId}:{ApplicationId}:", $"perm-effective:{TenantId}:{ApplicationId}:"];
}

public sealed record CreateGroupPermissionAssignmentResult(Guid Id);
