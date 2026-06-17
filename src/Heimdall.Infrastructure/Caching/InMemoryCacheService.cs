using System.Collections.Concurrent;
using Heimdall.Domain.Interfaces;
using Heimdall.Infrastructure.Logging;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Heimdall.Infrastructure.Caching;

public class InMemoryCacheService : ICacheService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<InMemoryCacheService> _logger;
    private readonly IHeimdallMetricsService _metrics;
    private readonly TimeSpan _defaultTtl;
    private readonly ConcurrentDictionary<string, byte> _trackedKeys = new();

    public InMemoryCacheService(
        IMemoryCache cache,
        IConfiguration configuration,
        ILogger<InMemoryCacheService> logger,
        IHeimdallMetricsService metrics)
    {
        _cache = cache;
        _logger = logger;
        _metrics = metrics;

        var ttlSeconds = configuration.GetValue<int>("Cache:DefaultTtlSeconds", 300);
        ttlSeconds = Math.Clamp(ttlSeconds, 1, 3600);
        _defaultTtl = TimeSpan.FromSeconds(ttlSeconds);
    }

    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        var found = _cache.TryGetValue(key, out var value);

        if (found && value is T typedValue)
        {
            _metrics.RecordCacheAccess(hit: true);
            return Task.FromResult<T?>(typedValue);
        }

        _metrics.RecordCacheAccess(hit: false);
        return Task.FromResult<T?>(default);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default)
    {
        var ttl = expiry ?? _defaultTtl;

        var options = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(ttl)
            .RegisterPostEvictionCallback((evictedKey, _, _, _) =>
            {
                _trackedKeys.TryRemove(evictedKey.ToString()!, out _);
            });

        _cache.Set(key, value, options);
        _trackedKeys.TryAdd(key, 0);

        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken ct = default)
    {
        _cache.Remove(key);
        _trackedKeys.TryRemove(key, out _);

        return Task.CompletedTask;
    }

    public Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default)
    {
        var keysToRemove = _trackedKeys.Keys
            .Where(k => k.StartsWith(prefix, StringComparison.Ordinal))
            .ToList();

        foreach (var key in keysToRemove)
        {
            _cache.Remove(key);
            _trackedKeys.TryRemove(key, out _);
        }

        return Task.CompletedTask;
    }
}
