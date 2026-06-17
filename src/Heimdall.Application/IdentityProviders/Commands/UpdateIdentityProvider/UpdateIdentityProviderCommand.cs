using Heimdall.Domain.Enums;
using Heimdall.Domain.ValueObjects;
using MediatR;

namespace Heimdall.Application.IdentityProviders.Commands.UpdateIdentityProvider;

public sealed record UpdateIdentityProviderCommand(
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
    List<string>? AllowedAlgorithms,
    List<ClaimMapping>? ClaimMappings,
    int ClockSkewToleranceSeconds = 300) : IRequest<UpdateIdentityProviderResult>;

public sealed record UpdateIdentityProviderResult(
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
    DateTime? ModifiedAt,
    string? ModifiedBy);
