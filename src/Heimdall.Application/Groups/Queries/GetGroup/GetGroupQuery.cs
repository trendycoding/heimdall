using MediatR;

namespace Heimdall.Application.Groups.Queries.GetGroup;

public sealed record GetGroupQuery(Guid Id) : IRequest<GroupDto?>;

public sealed record GroupDto(
    Guid Id,
    Guid TenantId,
    Guid ApplicationId,
    string Name,
    string? Description,
    bool IsActive,
    DateTime CreatedAt,
    string CreatedBy,
    DateTime? ModifiedAt,
    string? ModifiedBy);
