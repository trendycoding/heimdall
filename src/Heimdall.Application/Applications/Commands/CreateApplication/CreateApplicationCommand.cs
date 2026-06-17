using Heimdall.Domain.Enums;
using MediatR;

namespace Heimdall.Application.Applications.Commands.CreateApplication;

public sealed record CreateApplicationCommand(
    string Name,
    string ClientIdentifier,
    string? Description,
    List<string>? AllowedRedirectUris,
    List<string>? AllowedOrigins) : IRequest<CreateApplicationResult>;

public sealed record CreateApplicationResult(
    Guid ApplicationId,
    Guid TenantId,
    string Name,
    string? Description,
    string ClientIdentifier,
    List<string> AllowedRedirectUris,
    List<string> AllowedOrigins,
    ApplicationStatus Status,
    DateTime CreatedAt,
    string CreatedBy);
