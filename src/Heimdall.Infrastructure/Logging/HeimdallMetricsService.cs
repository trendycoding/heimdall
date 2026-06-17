using System.Diagnostics.Metrics;

namespace Heimdall.Infrastructure.Logging;

/// <summary>
/// Custom metrics service for Heimdall Access observability.
/// Tracks permission checks/sec, cache hit rate, request duration, and unhandled exceptions.
/// Uses System.Diagnostics.Metrics for Application Insights integration.
/// </summary>
public interface IHeimdallMetricsService
{
    /// <summary>Records a permission check operation.</summary>
    void RecordPermissionCheck(bool allowed, double durationMs);

    /// <summary>Records a cache access attempt with hit/miss result.</summary>
    void RecordCacheAccess(bool hit);

    /// <summary>Records request duration for a given endpoint.</summary>
    void RecordRequestDuration(string endpoint, string method, int statusCode, double durationMs);

    /// <summary>Records a dependency call duration (e.g., database, Redis, external APIs).</summary>
    void RecordDependencyDuration(string dependencyType, string target, bool success, double durationMs);

    /// <summary>Records an unhandled exception occurrence.</summary>
    void RecordUnhandledException(string exceptionType, string source);
}

/// <summary>
/// Implementation of custom metrics using System.Diagnostics.Metrics,
/// which integrates automatically with Application Insights.
/// </summary>
public sealed class HeimdallMetricsService : IHeimdallMetricsService
{
    public const string MeterName = "Heimdall.Access";

    private readonly Counter<long> _permissionChecksCounter;
    private readonly Counter<long> _permissionDeniedCounter;
    private readonly Histogram<double> _permissionCheckDuration;

    private readonly Counter<long> _cacheHitsCounter;
    private readonly Counter<long> _cacheMissesCounter;

    private readonly Histogram<double> _requestDuration;
    private readonly Histogram<double> _dependencyDuration;

    private readonly Counter<long> _unhandledExceptionsCounter;

    public HeimdallMetricsService(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);

        _permissionChecksCounter = meter.CreateCounter<long>(
            "heimdall.permission_checks.total",
            unit: "{checks}",
            description: "Total number of permission checks performed");

        _permissionDeniedCounter = meter.CreateCounter<long>(
            "heimdall.permission_checks.denied",
            unit: "{checks}",
            description: "Total number of permission checks that resulted in Deny");

        _permissionCheckDuration = meter.CreateHistogram<double>(
            "heimdall.permission_checks.duration",
            unit: "ms",
            description: "Duration of permission check operations in milliseconds");

        _cacheHitsCounter = meter.CreateCounter<long>(
            "heimdall.cache.hits",
            unit: "{hits}",
            description: "Total number of cache hits");

        _cacheMissesCounter = meter.CreateCounter<long>(
            "heimdall.cache.misses",
            unit: "{misses}",
            description: "Total number of cache misses");

        _requestDuration = meter.CreateHistogram<double>(
            "heimdall.http.request_duration",
            unit: "ms",
            description: "HTTP request duration in milliseconds");

        _dependencyDuration = meter.CreateHistogram<double>(
            "heimdall.dependencies.duration",
            unit: "ms",
            description: "External dependency call duration in milliseconds");

        _unhandledExceptionsCounter = meter.CreateCounter<long>(
            "heimdall.exceptions.unhandled",
            unit: "{exceptions}",
            description: "Total number of unhandled exceptions");
    }

    public void RecordPermissionCheck(bool allowed, double durationMs)
    {
        _permissionChecksCounter.Add(1);
        if (!allowed)
        {
            _permissionDeniedCounter.Add(1);
        }
        _permissionCheckDuration.Record(durationMs);
    }

    public void RecordCacheAccess(bool hit)
    {
        if (hit)
            _cacheHitsCounter.Add(1);
        else
            _cacheMissesCounter.Add(1);
    }

    public void RecordRequestDuration(string endpoint, string method, int statusCode, double durationMs)
    {
        _requestDuration.Record(durationMs,
            new KeyValuePair<string, object?>("endpoint", endpoint),
            new KeyValuePair<string, object?>("method", method),
            new KeyValuePair<string, object?>("status_code", statusCode));
    }

    public void RecordDependencyDuration(string dependencyType, string target, bool success, double durationMs)
    {
        _dependencyDuration.Record(durationMs,
            new KeyValuePair<string, object?>("dependency_type", dependencyType),
            new KeyValuePair<string, object?>("target", target),
            new KeyValuePair<string, object?>("success", success));
    }

    public void RecordUnhandledException(string exceptionType, string source)
    {
        _unhandledExceptionsCounter.Add(1,
            new KeyValuePair<string, object?>("exception_type", exceptionType),
            new KeyValuePair<string, object?>("source", source));
    }
}
