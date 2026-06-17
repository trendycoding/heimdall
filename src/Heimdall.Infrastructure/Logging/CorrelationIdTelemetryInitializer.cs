using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Http;

namespace Heimdall.Infrastructure.Logging;

/// <summary>
/// Telemetry initializer that adds the CorrelationId from the current HTTP request
/// to all Application Insights telemetry items as a custom property.
/// </summary>
public class CorrelationIdTelemetryInitializer : ITelemetryInitializer
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CorrelationIdTelemetryInitializer(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public void Initialize(ITelemetry telemetry)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null)
            return;

        if (httpContext.Items.TryGetValue("CorrelationId", out var correlationIdObj)
            && correlationIdObj is string correlationId
            && !string.IsNullOrEmpty(correlationId))
        {
            if (telemetry is ISupportProperties propertied)
            {
                propertied.Properties["CorrelationId"] = correlationId;
            }
        }
    }
}
