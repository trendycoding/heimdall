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
    /// Registers cloud-agnostic telemetry: the custom metrics service, which is built on
    /// <see cref="System.Diagnostics.Metrics"/> and works with any OpenTelemetry-compatible
    /// exporter (Application Insights, CloudWatch, OTLP collector, etc.).
    /// </summary>
    public static IServiceCollection AddHeimdallTelemetry(this IServiceCollection services)
    {
        // Custom metrics service — vendor-neutral (System.Diagnostics.Metrics)
        services.AddSingleton<IHeimdallMetricsService, HeimdallMetricsService>();

        return services;
    }

    /// <summary>
    /// Registers Azure Application Insights-specific telemetry enrichment: the CorrelationId
    /// initializer and the sensitive-data redaction processor. Only call this when running on
    /// Azure with Application Insights configured (i.e. after AddApplicationInsightsTelemetry()).
    /// </summary>
    public static IServiceCollection AddAzureApplicationInsightsEnrichment(this IServiceCollection services)
    {
        // Telemetry initializer: adds CorrelationId to all telemetry items
        services.AddSingleton<ITelemetryInitializer, CorrelationIdTelemetryInitializer>();

        // Telemetry processor: redacts sensitive data from all log output
        services.AddApplicationInsightsTelemetryProcessor<SensitiveDataTelemetryProcessor>();

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
