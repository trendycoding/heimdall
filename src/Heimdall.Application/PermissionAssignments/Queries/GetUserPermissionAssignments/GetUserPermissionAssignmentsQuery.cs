using MediatR;

namespace Heimdall.Application.PermissionAssignments.Queries.GetUserPermissionAssignments;

public sealed record GetUserPermissionAssignmentsQuery(
    Guid UserProfileId,
    Guid ApplicationId) : IRequest<IReadOnlyList<UserPermissionAssignmentDto>>;

public sealed record UserPermissionAssignmentDto(
    Guid Id,
    Guid TenantId,
    Guid ApplicationId,
    Guid UserProfileId,
    Guid PermissionId,
    string Effect,
    DateTime? ValidFrom,
    DateTime? ValidTo,
    DateTime CreatedAt,
    string CreatedBy,
    DateTime? ModifiedAt,
    string? ModifiedBy);
