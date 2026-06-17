using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Heimdall.Api.Configuration;

/// <summary>
/// Configures ASP.NET Core rate limiting middleware with a fixed window limiter
/// partitioned by client IP address.
/// </summary>
public static class RateLimitingConfiguration
{
    /// <summary>
    /// The policy name for the global fixed window rate limiter.
    /// </summary>
    public const string GlobalPolicy = "global-fixed-window";

    /// <summary>
    /// Adds rate limiting services with a fixed window limiter configuration.
    /// 100 requests per 60 seconds per client IP.
    /// </summary>
    public static IServiceCollection AddRateLimitingConfiguration(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy(GlobalPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: GetClientIpAddress(httpContext),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 100,
                        Window = TimeSpan.FromSeconds(60),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    }));

            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.HttpContext.Response.ContentType = "application/json";

                var correlationId = context.HttpContext.Items.TryGetValue("CorrelationId", out var value)
                    && value is string id
                        ? id
                        : Guid.NewGuid().ToString();

                var envelope = new
                {
                    success = false,
                    data = (object?)null,
                    errors = new[]
                    {
                        new
                        {
                            code = "RateLimitExceeded",
                            message = "Too many requests. Please try again later.",
                            field = (string?)null
                        }
                    },
                    correlationId
                };

                await context.HttpContext.Response.WriteAsJsonAsync(envelope, cancellationToken);
            };
        });

        return services;
    }

    private static string GetClientIpAddress(HttpContext httpContext)
    {
        // Check X-Forwarded-For header first (for requests behind a reverse proxy)
        var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwardedFor))
        {
            // Take the first IP in the list (the original client)
            return forwardedFor.Split(',', StringSplitOptions.RemoveEmptyEntries)[0].Trim();
        }

        return httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
