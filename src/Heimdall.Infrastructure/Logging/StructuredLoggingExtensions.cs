using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Heimdall.Infrastructure.Logging;

/// <summary>
/// Extension methods for configuring structured logging and Application Insights
/// telemetry enrichment in the Heimdall platform.
/// </summary>
public static class StructuredLoggingExtensions
{
    /// <summary>
    /// Registers Application Insights telemetry initializers, processors, and custom metrics.
    /// Call this after AddApplicationInsightsTelemetry() in Program.cs.
    /// </summary>
    public static IServiceCollection AddHeimdallTelemetry(this IServiceCollection services)
    {
        // Telemetry initializer: adds CorrelationId to all telemetry items
        services.AddSingleton<ITelemetryInitializer, CorrelationIdTelemetryInitializer>();

        // Telemetry processor: redacts sensitive data from all log output
        services.AddApplicationInsightsTelemetryProcessor<SensitiveDataTelemetryProcessor>();

        // Custom metrics service
        services.AddSingleton<IHeimdallMetricsService, HeimdallMetricsService>();

        return services;
    }

    /// <summary>
    /// Configures structured logging with JSON console output including
    /// timestamp, severity, CorrelationId, source component, and message.
    /// </summary>
    public static ILoggingBuilder AddHeimdallStructuredLogging(this ILoggingBuilder builder)
    {
        builder.AddJsonConsole(options =>
        {
            options.IncludeScopes = true;
            options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
            options.UseUtcTimestamp = true;
            options.JsonWriterOptions = new System.Text.Json.JsonWriterOptions
            {
                Indented = false
            };
        });

        return builder;
    }
}
