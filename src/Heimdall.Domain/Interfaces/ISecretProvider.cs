namespace Heimdall.Domain.Interfaces;

/// <summary>
/// Cloud-agnostic abstraction for retrieving secrets (connection strings, API keys, etc.).
/// Implementations may be backed by Azure Key Vault, AWS Secrets Manager, environment
/// variables, or any other secret store.
/// </summary>
public interface ISecretProvider
{
    /// <summary>
    /// Retrieves a secret value by key, with retry and local caching.
    /// Returns null if the secret does not exist.
    /// </summary>
    Task<string?> GetSecretAsync(string secretKey, CancellationToken ct = default);

    /// <summary>
    /// Retrieves a secret synchronously from cache or the underlying store.
    /// Used during startup/configuration binding where async is not available.
    /// </summary>
    string? GetSecret(string secretKey);

    /// <summary>
    /// Invalidates a cached secret so the next access re-reads from the store.
    /// </summary>
    void InvalidateSecret(string secretKey);

    /// <summary>
    /// Verifies the secret store is reachable. Used by health checks.
    /// Returns true if reachable (or if no secret store is configured).
    /// </summary>
    Task<bool> IsHealthyAsync(CancellationToken ct = default);
}
