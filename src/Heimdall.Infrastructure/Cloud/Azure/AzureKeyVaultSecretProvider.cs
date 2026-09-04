using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Heimdall.Domain.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Heimdall.Infrastructure.Cloud.Azure;

/// <summary>
/// Azure Key Vault-backed <see cref="ISecretProvider"/>.
/// Provides resilient access to secrets with retry logic and a local memory cache
/// to avoid repeated network calls. On transient failures, retries with exponential
/// backoff; falls back to stale cache if all retries fail.
/// </summary>
public sealed class AzureKeyVaultSecretProvider : ISecretProvider
{
    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _secretsCache;
    private readonly ILogger<AzureKeyVaultSecretProvider> _logger;

    private static readonly TimeSpan DefaultCacheDuration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan StaleCacheDuration = TimeSpan.FromHours(1);
    private const int MaxRetries = 3;

    public AzureKeyVaultSecretProvider(
        IConfiguration configuration,
        IMemoryCache secretsCache,
        ILogger<AzureKeyVaultSecretProvider> logger)
    {
        _configuration = configuration;
        _secretsCache = secretsCache;
        _logger = logger;
    }

    public async Task<string?> GetSecretAsync(string secretKey, CancellationToken ct = default)
    {
        var cacheKey = $"secret:{secretKey}";

        if (_secretsCache.TryGetValue(cacheKey, out string? cachedValue))
        {
            return cachedValue;
        }

        for (var attempt = 0; attempt <= MaxRetries; attempt++)
        {
            try
            {
                ct.ThrowIfCancellationRequested();

                var value = _configuration[secretKey];
                if (value is not null)
                {
                    _secretsCache.Set(cacheKey, value, DefaultCacheDuration);
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
                    "Failed to retrieve secret {SecretKey}, attempt {Attempt}/{MaxRetries}. Retrying in {DelayMs}ms",
                    secretKey, attempt + 1, MaxRetries, delay.TotalMilliseconds);

                await Task.Delay(delay, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve secret {SecretKey} after all retries", secretKey);

                if (_secretsCache.TryGetValue($"{cacheKey}:stale", out string? staleValue))
                {
                    _logger.LogWarning("Returning stale cached value for secret {SecretKey}", secretKey);
                    return staleValue;
                }

                return null;
            }
        }

        return null;
    }

    public string? GetSecret(string secretKey)
    {
        var cacheKey = $"secret:{secretKey}";

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

    public void InvalidateSecret(string secretKey)
    {
        _secretsCache.Remove($"secret:{secretKey}");
    }

    public async Task<bool> IsHealthyAsync(CancellationToken ct = default)
    {
        var keyVaultUri = _configuration["KeyVault:Uri"];

        // Not configured is considered healthy (secrets come from config/env)
        if (string.IsNullOrWhiteSpace(keyVaultUri))
        {
            return true;
        }

        try
        {
            var client = new SecretClient(new Uri(keyVaultUri), new DefaultAzureCredential());

            await foreach (var _ in client.GetPropertiesOfSecretsAsync(ct))
            {
                break; // Reaching the first page confirms connectivity + auth
            }

            return true;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Key Vault health check timed out.");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Key Vault health check failed.");
            return false;
        }
    }
}
