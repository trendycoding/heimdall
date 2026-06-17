using System.Diagnostics;
using Heimdall.Domain.Interfaces;
using Heimdall.Infrastructure.Logging;
using Microsoft.Extensions.Logging;

namespace Heimdall.Infrastructure.Caching;

/// <summary>
/// A resilient wrapper around RedisCacheService that implements a circuit breaker pattern.
/// When Redis becomes unavailable, the circuit opens and the service falls back to
/// returning cache misses (forcing direct DB computation) until Redis recovers.
/// </summary>
public sealed class ResilientCacheService : ICacheService
{
    private readonly RedisCacheService _redis;
    private readonly ILogger<ResilientCacheService> _logger;
    private readonly IHeimdallMetricsService _metrics;

    // Circuit breaker state
    private volatile CircuitState _state = CircuitState.Closed;
    private int _failureCount;
    private DateTime _lastFailureTime = DateTime.MinValue;
    private DateTime _circuitOpenedAt = DateTime.MinValue;
    private readonly object _lock = new();

    // Circuit breaker configuration
    private const int FailureThreshold = 5;
    private static readonly TimeSpan OpenDuration = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan FailureWindow = TimeSpan.FromSeconds(60);

    private enum CircuitState
    {
        Closed,
        Open,
        HalfOpen
    }

    public ResilientCacheService(
        RedisCacheService redis,
        ILogger<ResilientCacheService> logger,
        IHeimdallMetricsService metrics)
    {
        _redis = redis;
        _logger = logger;
        _metrics = metrics;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        if (!IsCircuitAllowingRequests())
        {
            _metrics.RecordCacheAccess(hit: false);
            return default;
        }

        try
        {
            var result = await _redis.GetAsync<T>(key, ct);
            OnSuccess();
            return result;
        }
        catch (Exception ex)
        {
            OnFailure(ex, nameof(GetAsync));
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default)
    {
        if (!IsCircuitAllowingRequests())
            return;

        try
        {
            await _redis.SetAsync(key, value, expiry, ct);
            OnSuccess();
        }
        catch (Exception ex)
        {
            OnFailure(ex, nameof(SetAsync));
        }
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        if (!IsCircuitAllowingRequests())
            return;

        try
        {
            await _redis.RemoveAsync(key, ct);
            OnSuccess();
        }
        catch (Exception ex)
        {
            OnFailure(ex, nameof(RemoveAsync));
        }
    }

    public async Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default)
    {
        if (!IsCircuitAllowingRequests())
            return;

        try
        {
            await _redis.RemoveByPrefixAsync(prefix, ct);
            OnSuccess();
        }
        catch (Exception ex)
        {
            OnFailure(ex, nameof(RemoveByPrefixAsync));
        }
    }

    private bool IsCircuitAllowingRequests()
    {
        switch (_state)
        {
            case CircuitState.Closed:
                return true;

            case CircuitState.Open:
                // Check if the open duration has elapsed — transition to half-open
                if (DateTime.UtcNow - _circuitOpenedAt >= OpenDuration)
                {
                    lock (_lock)
                    {
                        if (_state == CircuitState.Open)
                        {
                            _state = CircuitState.HalfOpen;
                            _logger.LogInformation(
                                "Redis circuit breaker transitioning to HalfOpen — allowing probe request");
                        }
                    }
                    return true;
                }
                return false;

            case CircuitState.HalfOpen:
                // Allow a single probe request through
                return true;

            default:
                return false;
        }
    }

    private void OnSuccess()
    {
        if (_state == CircuitState.HalfOpen)
        {
            lock (_lock)
            {
                _state = CircuitState.Closed;
                _failureCount = 0;
                _logger.LogInformation("Redis circuit breaker closed — Redis is healthy again");
                _metrics.RecordDependencyDuration("Redis", "CircuitBreaker", true, 0);
            }
        }
        else if (_failureCount > 0)
        {
            // Reset failure count on success in closed state if window elapsed
            lock (_lock)
            {
                if (DateTime.UtcNow - _lastFailureTime >= FailureWindow)
                {
                    _failureCount = 0;
                }
            }
        }
    }

    private void OnFailure(Exception ex, string operation)
    {
        _logger.LogWarning(ex, "Redis operation {Operation} failed — circuit breaker tracking failure", operation);
        _metrics.RecordDependencyDuration("Redis", operation, false, 0);

        lock (_lock)
        {
            _lastFailureTime = DateTime.UtcNow;

            // Reset count if outside failure window
            if (DateTime.UtcNow - _lastFailureTime >= FailureWindow)
            {
                _failureCount = 1;
            }
            else
            {
                _failureCount++;
            }

            if (_failureCount >= FailureThreshold && _state != CircuitState.Open)
            {
                _state = CircuitState.Open;
                _circuitOpenedAt = DateTime.UtcNow;
                _logger.LogError(
                    "Redis circuit breaker OPENED after {FailureCount} failures — falling back to direct DB computation for {Duration}s",
                    _failureCount, OpenDuration.TotalSeconds);
            }
        }
    }
}
