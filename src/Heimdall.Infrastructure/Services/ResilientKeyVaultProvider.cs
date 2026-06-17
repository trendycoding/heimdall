using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Heimdall.Infrastructure.Services;

/// <summary>
/// Provides resilient access to Azure Key Vault secrets with retry logic
/// and a local memory cache to avoid repeated network calls for the same secrets.
/// 
/// Secrets are cached in-process for a configurable duration (default 5 minutes).
/// On transient failures, requests are retried with exponential backoff (3 retries).
/// If all retries fail and a cached value exists, the stale cached value is returned.
/// </summary>
public sealed class ResilientKeyVaultProvider
{
    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _secretsCache;
    private readonly ILogger<ResilientKeyVaultProvider> _logger;

    private static readonly TimeSpan DefaultCacheDuration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan StaleCacheDuration = TimeSpan.FromHours(1);
    private const int MaxRetries = 3;

    public ResilientKeyVaultProvider(
        IConfiguration configuration,
        IMemoryCache secretsCache,
        ILogger<ResilientKeyVaultProvider> logger)
    {
        _configuration = configuration;
        _secretsCache = secretsCache;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves a secret value by key. First checks local memory cache,
    /// then falls back to configuration (which reads from Key Vault).
    /// Implements retry with exponential backoff on failures.
    /// </summary>
    public async Task<string?> GetSecretAsync(string secretKey, CancellationToken ct = default)
    {
        var cacheKey = $"kv-secret:{secretKey}";

        // Check local memory cache first
        if (_secretsCache.TryGetValue(cacheKey, out string? cachedValue))
        {
            return cachedValue;
        }

        // Attempt to read from configuration (backed by Key Vault) with retry
        for (var attempt = 0; attempt <= MaxRetries; attempt++)
        {
            try
            {
                ct.ThrowIfCancellationRequested();

                var value = _configuration[secretKey];
                if (value is not null)
                {
                    // Cache the secret locally
                    _secretsCache.Set(cacheKey, value, DefaultCacheDuration);

                    // Also store in stale cache for fallback
                    _secretsCache.Set($"{cacheKey}:stale", value, StaleCacheDuration);
                }

                return value;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (attempt < MaxRetries)
            {
                var delay = TimeSpan.FromMilliseconds(200 * Math.Pow(2, attempt));
                _logger.LogWarning(ex,
                    "Failed to retrieve secret {SecretKey} from Key Vault, attempt {Attempt}/{MaxRetries}. Retrying in {DelayMs}ms",
                    secretKey, attempt + 1, MaxRetries, delay.TotalMilliseconds);

                await Task.Delay(delay, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to retrieve secret {SecretKey} from Key Vault after all retries",
                    secretKey);

                // Fall back to stale cache if available
                if (_secretsCache.TryGetValue($"{cacheKey}:stale", out string? staleValue))
                {
                    _logger.LogWarning(
                        "Returning stale cached value for secret {SecretKey} after Key Vault failure",
                        secretKey);
                    return staleValue;
                }

                return null;
            }
        }

        return null;
    }

    /// <summary>
    /// Retrieves a secret synchronously from the local cache or configuration.
    /// Does not perform retry (used during startup/configuration binding).
    /// </summary>
    public string? GetSecret(string secretKey)
    {
        var cacheKey = $"kv-secret:{secretKey}";

        if (_secretsCache.TryGetValue(cacheKey, out string? cachedValue))
        {
            return cachedValue;
        }

        var value = _configuration[secretKey];
        if (value is not null)
        {
            _secretsCache.Set(cacheKey, value, DefaultCacheDuration);
            _secretsCache.Set($"{cacheKey}:stale", value, StaleCacheDuration);
        }

        return value;
    }

    /// <summary>
    /// Invalidates a cached secret, forcing the next access to re-read from Key Vault.
    /// </summary>
    public void InvalidateSecret(string secretKey)
    {
        var cacheKey = $"kv-secret:{secretKey}";
        _secretsCache.Remove(cacheKey);
    }
}
