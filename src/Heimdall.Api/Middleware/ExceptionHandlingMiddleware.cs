using System.Net;
using System.Text.Json;
using Heimdall.Infrastructure.Logging;

namespace Heimdall.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHeimdallMetricsService _metrics;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHeimdallMetricsService metrics)
    {
        _next = next;
        _logger = logger;
        _metrics = metrics;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception processing request {Method} {Path}",
                context.Request.Method, context.Request.Path);

            _metrics.RecordUnhandledException(
                ex.GetType().Name,
                $"{context.Request.Method} {context.Request.Path}");

            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, errorCode) = exception switch
        {
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "Unauthorized"),
            InvalidOperationException => (HttpStatusCode.BadRequest, "InvalidOperation"),
            ArgumentException => (HttpStatusCode.BadRequest, "ValidationFailed"),
            KeyNotFoundException => (HttpStatusCode.NotFound, "ResourceNotFound"),
            _ => (HttpStatusCode.InternalServerError, "InternalError")
        };

        var correlationId = context.Items.TryGetValue("CorrelationId", out var id)
            ? id?.ToString()
            : null;

        // Sanitize message — never expose internal details for 500 errors
        var message = statusCode == HttpStatusCode.InternalServerError
            ? "An unexpected error occurred. Please try again later."
            : exception.Message;

        var envelope = new
        {
            success = false,
            data = (object?)null,
            errors = new[]
            {
                new { code = errorCode, message }
            },
            correlationId
        };

        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json";

        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        await context.Response.WriteAsync(JsonSerializer.Serialize(envelope, options));
    }
}
