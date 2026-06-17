using MediatR;

namespace Heimdall.Application.PermissionAssignments.Queries.GetGroupPermissionAssignments;

public sealed record GetGroupPermissionAssignmentsQuery(
    Guid GroupId,
    Guid ApplicationId) : IRequest<IReadOnlyList<GroupPermissionAssignmentDto>>;

public sealed record GroupPermissionAssignmentDto(
    Guid Id,
    Guid TenantId,
    Guid ApplicationId,
    Guid GroupId,
    Guid PermissionId,
    string Effect,
    DateTime? ValidFrom,
    DateTime? ValidTo,
    DateTime CreatedAt,
    string CreatedBy,
    DateTime? ModifiedAt,
    string? ModifiedBy);
