using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace Heimdall.Api.HealthChecks;

/// <summary>
/// Health check that verifies Redis connectivity by issuing a PING command.
/// Only registered when Redis is configured; returns Unhealthy if Redis is unreachable.
/// </summary>
public sealed class RedisHealthCheck : IHealthCheck
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisHealthCheck> _logger;

    public RedisHealthCheck(IConnectionMultiplexer redis, ILogger<RedisHealthCheck> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var database = _redis.GetDatabase();
            var latency = await database.PingAsync();

            if (latency > TimeSpan.FromSeconds(2))
            {
                return HealthCheckResult.Degraded(
                    $"Redis is reachable but slow (latency: {latency.TotalMilliseconds:F0}ms).");
            }

            return HealthCheckResult.Healthy(
                $"Redis is accessible (latency: {latency.TotalMilliseconds:F0}ms).");
        }
        catch (OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy("Redis health check timed out.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis health check failed");
            return HealthCheckResult.Unhealthy(
                $"Redis is unreachable: {ex.Message}",
                exception: ex);
        }
    }
}
