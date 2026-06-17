using Heimdall.Domain.Enums;
using Heimdall.Domain.ValueObjects;
using MediatR;

namespace Heimdall.Application.IdentityProviders.Queries.GetIdentityProvider;

public sealed record GetIdentityProviderQuery(
    Guid ProviderId) : IRequest<IdentityProviderDto?>;

public sealed record IdentityProviderDto(
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
    string CreatedBy,
    DateTime? ModifiedAt,
    string? ModifiedBy);
