using Heimdall.Domain.Enums;

namespace Heimdall.Domain.Entities;

/// <summary>
/// Represents a registered API key for headless/machine-to-machine access.
/// API keys are bound to a tenant and carry a set of admin scopes.
/// The actual key value is never stored — only a SHA-256 hash for validation.
/// </summary>
public class ApiKeyRegistration : BaseEntity
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string KeyHash { get; set; } = string.Empty;
    public string KeyPrefix { get; set; } = string.Empty;
    public List<string> Scopes { get; set; } = new();
    public ApiKeyStatus Status { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
}
