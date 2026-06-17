using MediatR;

namespace Heimdall.Application.PermissionTypes.Queries.GetPermissionType;

public sealed record GetPermissionTypeQuery(Guid Id) : IRequest<PermissionTypeDto?>;

public sealed record PermissionTypeDto(
    Guid Id,
    Guid TenantId,
    Guid ApplicationId,
    string Code,
    string Name,
    string? Description,
    bool IsSystemReserved,
    bool IsActive,
    DateTime CreatedAt,
    string CreatedBy,
    DateTime? ModifiedAt,
    string? ModifiedBy);
