using Heimdall.Domain.Enums;
using MediatR;

namespace Heimdall.Application.Applications.Queries.GetApplications;

public sealed record GetApplicationsQuery : IRequest<IReadOnlyList<GetApplicationsResult>>;

public sealed record GetApplicationsResult(
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
