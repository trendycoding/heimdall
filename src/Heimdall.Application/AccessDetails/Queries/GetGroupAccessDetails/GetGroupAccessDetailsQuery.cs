using MediatR;

namespace Heimdall.Application.AccessDetails.Queries.GetGroupAccessDetails;

public sealed record GetGroupAccessDetailsQuery(
    Guid GroupId,
    Guid ApplicationId) : IRequest<IReadOnlyList<GroupAccessDetailDto>>;

public sealed record GroupAccessDetailDto(
    Guid Id,
    Guid TenantId,
    Guid ApplicationId,
    Guid GroupId,
    string AccessDetailType,
    string AccessDetailCode,
    string AccessDetailValue,
    string? Description,
    bool IsActive,
    DateTime? ValidFrom,
    DateTime? ValidTo,
    DateTime CreatedAt,
    string CreatedBy,
    DateTime? ModifiedAt,
    string? ModifiedBy);
