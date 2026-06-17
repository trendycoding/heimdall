using MediatR;

namespace Heimdall.Application.AccessDetails.Queries.GetFunctionalAreaAccessRequirements;

public sealed record GetFunctionalAreaAccessRequirementsQuery(
    Guid FunctionalAreaId) : IRequest<IReadOnlyList<FunctionalAreaAccessRequirementDto>>;

public sealed record FunctionalAreaAccessRequirementDto(
    Guid Id,
    Guid TenantId,
    Guid ApplicationId,
    Guid FunctionalAreaId,
    string AccessDetailType,
    bool IsRequired,
    string? Description,
    bool IsActive,
    DateTime CreatedAt,
    string CreatedBy,
    DateTime? ModifiedAt,
    string? ModifiedBy);
