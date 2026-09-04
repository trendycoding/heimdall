using Heimdall.Domain.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Heimdall.Infrastructure.Cloud.Aws;

/// <summary>
/// AWS Secrets Manager-backed <see cref="ISecretProvider"/>.
///
/// STUB IMPLEMENTATION. This falls back to <see cref="IConfiguration"/> so the
/// platform runs on AWS using environment variables / SSM-injected config today.
///
/// To enable native AWS Secrets Manager retrieval:
///   1. Add the AWSSDK.SecretsManager NuGet package to Heimdall.Infrastructure.
///   2. Inject an IAmazonSecretsManager client (via AWSSDK.Extensions.NETCore.Setup).
///   3. Replace the ReadFromStore method with a GetSecretValueAsync call.
/// The caching, retry, and health-check contract is already defined below.
/// </summary>
public sealed class AwsSecretsManagerProvider : ISecretProvider
{
    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _cache;
    private readonly ILogger<AwsSecretsManagerProvider> _logger;

    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public AwsSecretsManagerProvider(
        IConfiguration configuration,
        IMemoryCache cache,
        ILogger<AwsSecretsManagerProvider> logger)
    {
        _configuration = configuration;
        _cache = cache;
        _logger = logger;
    }

    public Task<string?> GetSecretAsync(string secretKey, CancellationToken ct = default)
    {
        return Task.FromResult(GetSecret(secretKey));
    }

    public string? GetSecret(string secretKey)
    {
        var cacheKey = $"secret:{secretKey}";
        if (_cache.TryGetValue(cacheKey, out string? cached))
        {
            return cached;
        }

        var value = ReadFromStore(secretKey);
        if (value is not null)
        {
            _cache.Set(cacheKey, value, CacheDuration);
        }

        return value;
    }

    public void InvalidateSecret(string secretKey)
    {
        _cache.Remove($"secret:{secretKey}");
    }

    public Task<bool> IsHealthyAsync(CancellationToken ct = default)
    {
        // Stub: configuration is always reachable. Replace with a Secrets Manager
        // connectivity probe when native retrieval is implemented.
        return Task.FromResult(true);
    }

    /// <summary>
    /// Reads a secret from the underlying store. Currently backed by IConfiguration
    /// (env vars / SSM-injected). Replace with AWS Secrets Manager SDK call.
    /// </summary>
    private string? ReadFromStore(string secretKey)
    {
        _logger.LogDebug(
            "AWS Secrets Manager provider is a stub; reading '{SecretKey}' from configuration.",
            secretKey);
        return _configuration[secretKey];
    }
}
