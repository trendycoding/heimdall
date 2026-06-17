using MediatR;

namespace Heimdall.Application.FunctionalAreas.Queries.GetFunctionalArea;

public sealed record GetFunctionalAreaQuery(Guid Id) : IRequest<FunctionalAreaDto?>;

public sealed record FunctionalAreaDto(
    Guid Id,
    Guid TenantId,
    Guid ApplicationId,
    string FunctionalAreaCode,
    string Name,
    string? Description,
    bool IsActive,
    DateTime CreatedAt,
    string CreatedBy,
    DateTime? ModifiedAt,
    string? ModifiedBy);
