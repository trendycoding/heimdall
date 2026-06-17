using Heimdall.Domain.Enums;
using Heimdall.Domain.ValueObjects;
using MediatR;

namespace Heimdall.Application.IdentityProviders.Commands.CreateIdentityProvider;

public sealed record CreateIdentityProviderCommand(
    Guid TenantId,
    Guid? ApplicationId,
    ProviderType ProviderType,
    string Name,
    string Issuer,
    string? Audience,
    string? ClientId,
    string? JwksEndpoint,
    string? SamlMetadataUrl,
    List<string>? AllowedAlgorithms,
    List<ClaimMapping>? ClaimMappings,
    int ClockSkewToleranceSeconds = 300,
    IdpStatus Status = IdpStatus.Active) : IRequest<CreateIdentityProviderResult>;

public sealed record CreateIdentityProviderResult(
    Guid ProviderId,
    Guid TenantId,
    Guid? ApplicationId,
    ProviderType ProviderType,
    string Name,
    string Issuer,
    string? Audience,
    string? ClientId,
    string? JwksEndpoint,
    string? SamlMetadataUrl,
    List<string> AllowedAlgorithms,
    List<ClaimMapping> ClaimMappings,
    int ClockSkewToleranceSeconds,
    IdpStatus Status,
    DateTime CreatedAt,
    string CreatedBy);
