using System.Security.Claims;
using System.Text.Json;
using Heimdall.Domain.Interfaces;

namespace Heimdall.Api.Middleware;

public class TokenValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TokenValidationMiddleware> _logger;

    /// <summary>
    /// Paths that bypass token validation (e.g., health checks, Swagger).
    /// </summary>
    private static readonly string[] ExcludedPaths =
    [
        "/health",
        "/swagger",
        "/favicon.ico"
    ];

    public TokenValidationMiddleware(RequestDelegate next, ILogger<TokenValidationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ITokenValidationService tokenValidationService)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        if (IsExcludedPath(path))
        {
            await _next(context);
            return;
        }

        // Try bearer token first
        var authHeader = context.Request.Headers.Authorization.ToString();
        string? token = null;

        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            token = authHeader["Bearer ".Length..].Trim();
        }

        // Try API key header as alternative
        var apiKey = context.Request.Headers["X-Api-Key"].ToString();

        if (string.IsNullOrEmpty(token) && string.IsNullOrEmpty(apiKey))
        {
            await WriteUnauthorizedResponse(context, "No valid authentication credentials provided.");
            return;
        }

        if (!string.IsNullOrEmpty(token))
        {
            // Resolve tenantId from route if available for token validation context
            var tenantId = ResolveTenantIdFromRoute(context);

            var result = await tokenValidationService.ValidateTokenAsync(token, tenantId ?? Guid.Empty);

            if (!result.IsValid)
            {
                _logger.LogWarning("Token validation failed: {Error}", result.Error);
                await WriteUnauthorizedResponse(context, result.Error ?? "Invalid token.");
                return;
            }

            // Set claims principal from validated token
            var claims = result.Claims.Select(c => new Claim(c.Key, c.Value)).ToList();
            var identity = new ClaimsIdentity(claims, "Bearer");
            context.User = new ClaimsPrincipal(identity);
            context.Items["TokenClaims"] = result.Claims;
        }
        else if (!string.IsNullOrEmpty(apiKey))
        {
            // API key validation: store key for downstream tenant resolution
            context.Items["ApiKey"] = apiKey;
            context.Items["AuthMethod"] = "ApiKey";
        }

        await _next(context);
    }

    private static bool IsExcludedPath(string path)
    {
        return ExcludedPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase));
    }

    private static Guid? ResolveTenantIdFromRoute(HttpContext context)
    {
        if (context.Request.RouteValues.TryGetValue("tenantId", out var routeValue)
            && Guid.TryParse(routeValue?.ToString(), out var tenantId))
        {
            return tenantId;
        }
        return null;
    }

    private static async Task WriteUnauthorizedResponse(HttpContext context, string message)
    {
        var correlationId = context.Items.TryGetValue("CorrelationId", out var id) ? id?.ToString() : null;

        var envelope = new
        {
            success = false,
            data = (object?)null,
            errors = new[]
            {
                new { code = "Unauthorized", message }
            },
            correlationId
        };

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/json";

        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        await context.Response.WriteAsync(JsonSerializer.Serialize(envelope, options));
    }
}
