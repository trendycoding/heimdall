using MediatR;

namespace Heimdall.Application.AccessDetails.Queries.GetUserAccessDetails;

public sealed record GetUserAccessDetailsQuery(
    Guid UserProfileId,
    Guid ApplicationId) : IRequest<IReadOnlyList<UserAccessDetailDto>>;

public sealed record UserAccessDetailDto(
    Guid Id,
    Guid TenantId,
    Guid ApplicationId,
    Guid UserProfileId,
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
