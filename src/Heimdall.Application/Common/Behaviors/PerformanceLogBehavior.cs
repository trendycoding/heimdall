using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Heimdall.Application.Common.Behaviors;

/// <summary>
/// Pipeline behavior that logs a warning when handler execution exceeds the configured threshold.
/// This is the outermost behavior and captures total pipeline execution time.
/// </summary>
public sealed class PerformanceLogBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<PerformanceLogBehavior<TRequest, TResponse>> _logger;
    private const int ThresholdMilliseconds = 500;

    public PerformanceLogBehavior(ILogger<PerformanceLogBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        var response = await next();

        stopwatch.Stop();

        if (stopwatch.ElapsedMilliseconds > ThresholdMilliseconds)
        {
            var requestName = typeof(TRequest).Name;

            _logger.LogWarning(
                "Long-running request: {RequestName} ({ElapsedMilliseconds}ms)",
                requestName,
                stopwatch.ElapsedMilliseconds);
        }

        return response;
    }
}
