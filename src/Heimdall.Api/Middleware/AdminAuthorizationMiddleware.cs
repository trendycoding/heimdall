using System.Text.Json;
using Heimdall.Domain.Interfaces;

namespace Heimdall.Api.Middleware;

public class AdminAuthorizationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AdminAuthorizationMiddleware> _logger;

    /// <summary>
    /// Paths that bypass admin authorization.
    /// </summary>
    private static readonly string[] ExcludedPaths =
    [
        "/health",
        "/swagger",
        "/favicon.ico",
        "/api/me"
    ];

    public AdminAuthorizationMiddleware(RequestDelegate next, ILogger<AdminAuthorizationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        if (IsExcludedPath(path))
        {
            await _next(context);
            return;
        }

        // If using API key auth (service-to-service), validate against key's scopes
        if (context.Items.ContainsKey("AuthMethod")
            && context.Items["AuthMethod"]?.ToString() == "ApiKey")
        {
            var requiredScopeForKey = GetRequiredScope(context);
            if (requiredScopeForKey is null)
            {
                await _next(context);
                return;
            }

            // Check if the API key has SecurityServiceAdmin (acts as super admin)
            if (tenantContext.IsSuperAdmin)
            {
                await _next(context);
                return;
            }

            // Check if the API key's scopes satisfy the requirement
            if (tenantContext.AdminScopes.Any(s => s.Equals(requiredScopeForKey, StringComparison.OrdinalIgnoreCase)))
            {
                await _next(context);
                return;
            }

            await WriteForbiddenResponse(context, $"API key does not have required scope: {requiredScopeForKey}");
            return;
        }

        var requiredScope = GetRequiredScope(context);

        if (requiredScope is null)
        {
            // No specific scope required for this endpoint
            await _next(context);
            return;
        }

        var callerScopes = tenantContext.AdminScopes;

        // SecurityServiceAdmin (SecurityService.Admin) satisfies any scope requirement
        if (tenantContext.IsSuperAdmin)
        {
            await _next(context);
            return;
        }

        if (!callerScopes.Any(s => s.Equals(requiredScope, StringComparison.OrdinalIgnoreCase)))
        {
            _logger.LogWarning(
                "Admin authorization denied. Required scope: {RequiredScope}, Caller scopes: {CallerScopes}, Actor: {ActorSubjectId}",
                requiredScope, string.Join(", ", callerScopes), tenantContext.ActorSubjectId);

            await WriteForbiddenResponse(context, $"Insufficient administrative permissions. Required scope: {requiredScope}");
            return;
        }

        await _next(context);
    }

    /// <summary>
    /// Determines the required admin scope based on the request path and method.
    /// Returns null if no specific scope is required.
    /// </summary>
    private static string? GetRequiredScope(HttpContext context)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? string.Empty;
        var method = context.Request.Method;

        // Access check endpoints — available to all authenticated callers
        if (path.Contains("/access-checks"))
            return null;

        // Tenant management
        if (path.Contains("/tenants") && !path.Contains("/tenants/") && method == "POST")
            return nameof(Domain.Enums.AdminScope.TenantAdmin);

        if (path.Contains("/tenants/"))
        {
            // Application management under tenant
            if (path.Contains("/applications"))
                return nameof(Domain.Enums.AdminScope.ApplicationAdmin);

            // Identity provider management
            if (path.Contains("/identity-providers"))
                return nameof(Domain.Enums.AdminScope.IdentityProviderAdmin);

            // User management
            if (path.Contains("/users"))
                return nameof(Domain.Enums.AdminScope.TenantAdmin);

            // Permission-related management
            if (path.Contains("/functional-areas") || path.Contains("/permission-types")
                || path.Contains("/permissions") || path.Contains("/permission-assignments"))
                return nameof(Domain.Enums.AdminScope.PermissionManager);

            // Group management
            if (path.Contains("/groups"))
                return nameof(Domain.Enums.AdminScope.PermissionManager);

            // Template management
            if (path.Contains("/permission-templates"))
                return nameof(Domain.Enums.AdminScope.TemplateManager);

            // Access detail management
            if (path.Contains("/access-details"))
                return nameof(Domain.Enums.AdminScope.AccessDetailManager);

            // Audit and API call logs — read-only operations
            if (path.Contains("/audit-logs") || path.Contains("/api-call-logs"))
                return nameof(Domain.Enums.AdminScope.Auditor);

            // General tenant update/delete
            if (method is "PUT" or "DELETE")
                return nameof(Domain.Enums.AdminScope.TenantAdmin);
        }

        // Default: ReadOnly scope for any authenticated GET requests
        if (method == "GET")
            return null;

        return null;
    }

    private static bool IsExcludedPath(string path)
    {
        return ExcludedPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase));
    }

    private static async Task WriteForbiddenResponse(HttpContext context, string message)
    {
        var correlationId = context.Items.TryGetValue("CorrelationId", out var id) ? id?.ToString() : null;

        var envelope = new
        {
            success = false,
            data = (object?)null,
            errors = new[]
            {
                new { code = "PermissionDenied", message }
            },
            correlationId
        };

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/json";

        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        await context.Response.WriteAsync(JsonSerializer.Serialize(envelope, options));
    }
}
