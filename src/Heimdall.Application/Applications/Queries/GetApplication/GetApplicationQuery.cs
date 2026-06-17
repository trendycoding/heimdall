using Heimdall.Domain.Enums;
using MediatR;

namespace Heimdall.Application.Applications.Queries.GetApplication;

public sealed record GetApplicationQuery(Guid ApplicationId) : IRequest<GetApplicationResult?>;

public sealed record GetApplicationResult(
    Guid ApplicationId,
    Guid TenantId,
    string Name,
    string? Description,
    string ClientIdentifier,
    List<string> AllowedRedirectUris,
    List<string> AllowedOrigins,
    ApplicationStatus Status,
    DateTime CreatedAt,
    string CreatedBy,
    DateTime? ModifiedAt,
    string? ModifiedBy);
