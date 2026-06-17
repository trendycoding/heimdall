using System.Text.Json;
using Heimdall.Domain.Interfaces;
using Heimdall.Infrastructure.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Heimdall.Infrastructure.Caching;

public class RedisCacheService : ICacheService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisCacheService> _logger;
    private readonly IHeimdallMetricsService _metrics;
    private readonly TimeSpan _defaultTtl;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public RedisCacheService(
        IConnectionMultiplexer redis,
        IConfiguration configuration,
        ILogger<RedisCacheService> logger,
        IHeimdallMetricsService metrics)
    {
        _redis = redis;
        _logger = logger;
        _metrics = metrics;

        var ttlSeconds = configuration.GetValue<int>("Cache:DefaultTtlSeconds", 300);
        ttlSeconds = Math.Clamp(ttlSeconds, 1, 3600);
        _defaultTtl = TimeSpan.FromSeconds(ttlSeconds);
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var value = await db.StringGetAsync(key);

        if (value.IsNullOrEmpty)
        {
            _metrics.RecordCacheAccess(hit: false);
            return default;
        }

        try
        {
            _metrics.RecordCacheAccess(hit: true);
            return JsonSerializer.Deserialize<T>(value!, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize cached value for key {CacheKey}", key);
            _metrics.RecordCacheAccess(hit: false);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var ttl = expiry ?? _defaultTtl;
        var serialized = JsonSerializer.Serialize(value, JsonOptions);

        await db.StringSetAsync(key, serialized, ttl);
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        await db.KeyDeleteAsync(key);
    }

    public async Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default)
    {
        var endpoints = _redis.GetEndPoints();

        foreach (var endpoint in endpoints)
        {
            var server = _redis.GetServer(endpoint);
            var keys = new List<RedisKey>();

            await foreach (var key in server.KeysAsync(pattern: $"{prefix}*"))
            {
                keys.Add(key);

                if (keys.Count >= 1000)
                {
                    var db = _redis.GetDatabase();
                    await db.KeyDeleteAsync(keys.ToArray());
                    keys.Clear();
                }
            }

            if (keys.Count > 0)
            {
                var db = _redis.GetDatabase();
                await db.KeyDeleteAsync(keys.ToArray());
            }
        }
    }
}
