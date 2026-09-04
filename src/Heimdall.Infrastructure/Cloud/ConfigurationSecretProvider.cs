using Heimdall.Domain.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;

namespace Heimdall.Infrastructure.Cloud;

/// <summary>
/// Cloud-agnostic <see cref="ISecretProvider"/> backed by <see cref="IConfiguration"/>.
/// Reads secrets from environment variables, appsettings, or any configured provider.
/// Used when <c>CloudProvider=None</c> (local development or self-hosted deployments).
/// </summary>
public sealed class ConfigurationSecretProvider : ISecretProvider
{
    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _cache;

    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public ConfigurationSecretProvider(IConfiguration configuration, IMemoryCache cache)
    {
        _configuration = configuration;
        _cache = cache;
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

        var value = _configuration[secretKey];
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
        // Configuration is always available in-process.
        return Task.FromResult(true);
    }
}
