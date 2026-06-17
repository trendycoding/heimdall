namespace Heimdall.Infrastructure.Logging;

/// <summary>
/// No-op implementation of IHeimdallMetricsService for use in tests or environments
/// where metrics collection is not required.
/// </summary>
public sealed class NullHeimdallMetricsService : IHeimdallMetricsService
{
    public static readonly NullHeimdallMetricsService Instance = new();

    public void RecordPermissionCheck(bool allowed, double durationMs) { }
    public void RecordCacheAccess(bool hit) { }
    public void RecordRequestDuration(string endpoint, string method, int statusCode, double durationMs) { }
    public void RecordDependencyDuration(string dependencyType, string target, bool success, double durationMs) { }
    public void RecordUnhandledException(string exceptionType, string source) { }
}
