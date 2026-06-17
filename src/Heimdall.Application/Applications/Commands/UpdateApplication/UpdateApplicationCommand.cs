using Heimdall.Domain.Enums;
using MediatR;

namespace Heimdall.Application.Applications.Commands.UpdateApplication;

public sealed record UpdateApplicationCommand(
    Guid ApplicationId,
    string Name,
    string? Description,
    List<string>? AllowedRedirectUris,
    List<string>? AllowedOrigins) : IRequest<UpdateApplicationResult>;

public sealed record UpdateApplicationResult(
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
