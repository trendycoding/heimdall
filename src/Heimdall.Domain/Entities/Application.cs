using Heimdall.Domain.Enums;

namespace Heimdall.Domain.Entities;

public class Application : TenantScopedEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ClientIdentifier { get; set; } = string.Empty;
    public List<string> AllowedRedirectUris { get; set; } = new();
    public List<string> AllowedOrigins { get; set; } = new();
    public ApplicationStatus Status { get; set; }
}
