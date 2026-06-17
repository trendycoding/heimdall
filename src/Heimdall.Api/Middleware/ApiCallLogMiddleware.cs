using System.Diagnostics;
using Heimdall.Domain.Interfaces;
using Heimdall.Domain.Models;
using Heimdall.Infrastructure.Logging;

namespace Heimdall.Api.Middleware;

public class ApiCallLogMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiCallLogMiddleware> _logger;

    public ApiCallLogMiddleware(RequestDelegate next, ILogger<ApiCallLogMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IApiCallLogService apiCallLogService, IHeimdallMetricsService metrics)
    {
        var stopwatch = Stopwatch.StartNew();
        Guid? apiCallLogId = null;

        try
        {
            var correlationId = context.Items.TryGetValue("CorrelationId", out var id)
                ? Guid.TryParse(id?.ToString(), out var parsed) ? parsed : Guid.Empty
                : Guid.Empty;

            var tenantId = context.Items.TryGetValue("TenantId", out var tid)
                ? Guid.TryParse(tid?.ToString(), out var parsedTid) ? parsedTid : Guid.Empty
                : Guid.Empty;

            var actorSubjectId = context.User?.FindFirst("sub")?.Value;
            var callerClientId = context.User?.FindFirst("client_id")?.Value
                                ?? context.User?.FindFirst("azp")?.Value
                                ?? (context.Items.TryGetValue("ClientId", out var cid) ? cid?.ToString() : null);

            var entry = new ApiCallLogEntry
            {
                TenantId = tenantId,
                ApplicationId = null,
                HttpMethod = context.Request.Method,
                RequestPath = context.Request.Path.Value ?? string.Empty,
                Endpoint = context.Request.Path.Value ?? string.Empty,
                QueryString = context.Request.QueryString.HasValue ? context.Request.QueryString.Value : null,
                ClientIpAddress = context.Connection.RemoteIpAddress?.ToString(),
                UserAgent = context.Request.Headers.UserAgent.ToString(),
                ActorSubjectId = actorSubjectId,
                CallerClientId = callerClientId,
                CorrelationId = correlationId,
                RequestedAt = DateTime.UtcNow
            };

            apiCallLogId = await apiCallLogService.BeginLogAsync(entry);
            context.Items["ApiCallLogId"] = apiCallLogId;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to begin API call log. Continuing request processing.");
        }

        await _next(context);

        stopwatch.Stop();

        // Record request duration metric
        metrics.RecordRequestDuration(
            context.Request.Path.Value ?? "unknown",
            context.Request.Method,
            context.Response.StatusCode,
            stopwatch.Elapsed.TotalMilliseconds);

        if (apiCallLogId.HasValue)
        {
            try
            {
                await apiCallLogService.CompleteLogAsync(
                    apiCallLogId.Value,
                    context.Response.StatusCode,
                    stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to complete API call log for {ApiCallLogId}. Continuing.", apiCallLogId.Value);
            }
        }
    }
}
