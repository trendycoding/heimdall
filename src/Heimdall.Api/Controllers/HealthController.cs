using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Heimdall.Api.Controllers;

/// <summary>
/// Health check endpoint verifying database, cache (Redis if enabled), and Key Vault accessibility.
/// Uses ASP.NET Core Health Checks infrastructure with a 5-second timeout.
/// Mapped via MapHealthChecks in Program.cs — this controller is kept for documentation/discovery only.
/// The actual endpoint is served by the health checks middleware.
/// </summary>
public static class HealthCheckConfiguration
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    /// <summary>
    /// Configures the /health endpoint using ASP.NET Core Health Checks middleware.
    /// </summary>
    public static IEndpointRouteBuilder MapHeimdallHealthChecks(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = WriteHealthCheckResponse,
            ResultStatusCodes =
            {
                [HealthStatus.Healthy] = StatusCodes.Status200OK,
                [HealthStatus.Degraded] = StatusCodes.Status200OK,
                [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
            }
        }).AllowAnonymous();

        return endpoints;
    }

    /// <summary>
    /// Writes a JSON response with individual component statuses.
    /// </summary>
    private static async Task WriteHealthCheckResponse(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var response = new HealthCheckResponse
        {
            Status = report.Status.ToString(),
            TotalDurationMs = report.TotalDuration.TotalMilliseconds,
            Checks = report.Entries.Select(entry => new HealthCheckEntry
            {
                Name = entry.Key,
                Status = entry.Value.Status.ToString(),
                Description = entry.Value.Description,
                DurationMs = entry.Value.Duration.TotalMilliseconds,
                Error = entry.Value.Exception?.Message
            }).ToList()
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response, JsonOptions));
    }
}

/// <summary>
/// Health check JSON response model.
/// </summary>
public sealed class HealthCheckResponse
{
    public string Status { get; init; } = "Healthy";
    public double TotalDurationMs { get; init; }
    public List<HealthCheckEntry> Checks { get; init; } = new();
}

/// <summary>
/// Individual health check entry in the response.
/// </summary>
public sealed class HealthCheckEntry
{
    public string Name { get; init; } = string.Empty;
    public string Status { get; init; } = "Healthy";
    public string? Description { get; init; }
    public double DurationMs { get; init; }
    public string? Error { get; init; }
}
