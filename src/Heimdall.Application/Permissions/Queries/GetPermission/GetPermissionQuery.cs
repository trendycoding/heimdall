using MediatR;

namespace Heimdall.Application.Permissions.Queries.GetPermission;

public sealed record GetPermissionQuery(Guid Id) : IRequest<PermissionDto?>;

public sealed record PermissionDto(
    Guid Id,
    Guid TenantId,
    Guid ApplicationId,
    Guid FunctionalAreaId,
    Guid PermissionTypeId,
    string PermissionCode,
    string Name,
    string? Description,
    bool IsActive,
    DateTime CreatedAt,
    string CreatedBy,
    DateTime? ModifiedAt,
    string? ModifiedBy);
