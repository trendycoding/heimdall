namespace Heimdall.Domain.Models;

public sealed class ApiKeyValidationResult
{
    public bool IsValid { get; init; }
    public Guid TenantId { get; init; }
    public Guid ApiKeyId { get; init; }
    public string KeyName { get; init; } = string.Empty;
    public IReadOnlyList<string> Scopes { get; init; } = Array.Empty<string>();
    public string? Error { get; init; }
}
