using Heimdall.Domain.Enums;
using Heimdall.Domain.ValueObjects;

namespace Heimdall.Domain.Entities;

public class IdentityProviderConfiguration : TenantScopedEntity
{
    public Guid? ApplicationId { get; set; }
    public ProviderType ProviderType { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string? Audience { get; set; }
    public string? ClientId { get; set; }
    public string? JwksEndpoint { get; set; }
    public string? SamlMetadataUrl { get; set; }
    public List<string> AllowedAlgorithms { get; set; } = new();
    public List<ClaimMapping> ClaimMappings { get; set; } = new();
    public int ClockSkewToleranceSeconds { get; set; } = 300;
    public IdpStatus Status { get; set; }
}
