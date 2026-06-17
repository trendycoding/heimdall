using Heimdall.Application.Common.Interfaces;
using Heimdall.Domain.Enums;
using Heimdall.Domain.Interfaces;
using MediatR;

namespace Heimdall.Application.PermissionAssignments.Commands.CreateUserPermissionAssignment;

/// <summary>
/// Creates a user permission assignment. Invalidates the user's permission and effective permission cache.
/// </summary>
public sealed class CreateUserPermissionAssignmentCommand : IRequest<CreateUserPermissionAssignmentResult>, ICacheInvalidatingCommand
{
    public Guid TenantId { get; init; }
    public Guid ApplicationId { get; init; }
    public Guid UserProfileId { get; init; }
    public Guid PermissionId { get; init; }
    public Effect Effect { get; init; }
    public DateTime? ValidFrom { get; init; }
    public DateTime? ValidTo { get; init; }

    // ICacheInvalidatingCommand
    public IReadOnlyList<string> GetCacheKeyPrefixes()
        => [$"perm:{TenantId}:{ApplicationId}:{UserProfileId}:", $"perm-effective:{TenantId}:{ApplicationId}:{UserProfileId}"];
}

public sealed record CreateUserPermissionAssignmentResult(Guid Id);
