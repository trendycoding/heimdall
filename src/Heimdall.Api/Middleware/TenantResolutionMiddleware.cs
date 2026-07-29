using System.Text.Json;
using Heimdall.Domain.Interfaces;
using Heimdall.Infrastructure.Identity;

namespace Heimdall.Api.Middleware;

public class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantResolutionMiddleware> _logger;

    /// <summary>
    /// Paths that bypass tenant resolution.
    /// </summary>
    private static readonly string[] ExcludedPaths =
    [
        "/health",
        "/swagger",
        "/favicon.ico",
        "/api/me"
    ];

    public TenantResolutionMiddleware(RequestDelegate next, ILogger<TenantResolutionMiddleware> logger)
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

        var resolvedTenantId = ResolveTenantId(context);

        if (resolvedTenantId is null)
        {
            _logger.LogWarning("Tenant resolution failed for request {Method} {Path}",
                context.Request.Method, context.Request.Path);

            await WriteForbiddenResponse(context, "Unable to resolve tenant from request.");
            return;
        }

        // Verify cross-tenant authorization if route tenant differs from token tenant
        var tokenTenantId = GetTenantIdFromToken(context);
        var isSuperAdmin = IsSuperAdmin(context);

        if (tokenTenantId.HasValue && resolvedTenantId != tokenTenantId && !isSuperAdmin)
        {
            _logger.LogWarning(
                "Cross-tenant access denied. Resolved tenant: {ResolvedTenantId}, Token tenant: {TokenTenantId}",
                resolvedTenantId, tokenTenantId);

            await WriteForbiddenResponse(context, "Cross-tenant access is not authorized.");
            return;
        }

        // Populate the TenantContext for downstream use
        var mutableContext = (TenantContext)tenantContext;
        mutableContext.TenantId = resolvedTenantId.Value;

        // Set actor info from claims
        var subClaim = context.User?.FindFirst("sub")?.Value;
        var emailClaim = context.User?.FindFirst("email")?.Value;
        var scopesClaim = context.User?.FindFirst("scopes")?.Value
                          ?? context.User?.FindFirst("scope")?.Value;

        mutableContext.ActorSubjectId = subClaim ?? string.Empty;
        mutableContext.ActorEmail = emailClaim ?? string.Empty;
        mutableContext.IsSuperAdmin = isSuperAdmin;

        if (!string.IsNullOrEmpty(scopesClaim))
        {
            mutableContext.AdminScopes = scopesClaim
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .ToList()
                .AsReadOnly();
        }

        // If API key auth, populate scopes from the validated key
        if (context.Items.TryGetValue("AuthMethod", out var authMethod)
            && authMethod?.ToString() == "ApiKey")
        {
            if (context.Items.TryGetValue("ApiKeyScopes", out var apiKeyScopes)
                && apiKeyScopes is IReadOnlyList<string> scopes)
            {
                mutableContext.AdminScopes = scopes;
            }

            var keyName = context.Items.TryGetValue("ApiKeyName", out var kn) ? kn?.ToString() : "api-key";
            mutableContext.ActorSubjectId = $"apikey:{keyName}";
            mutableContext.ActorEmail = $"apikey:{keyName}";

            // API keys with SecurityServiceAdmin scope are super admins
            if (mutableContext.AdminScopes.Any(s =>
                s.Equals("SecurityServiceAdmin", StringComparison.OrdinalIgnoreCase)
                || s.Equals("SecurityService.Admin", StringComparison.OrdinalIgnoreCase)))
            {
                mutableContext.IsSuperAdmin = true;
            }
        }

        context.Items["TenantId"] = resolvedTenantId.Value.ToString();

        await _next(context);
    }

    /// <summary>
    /// Resolves tenant ID with priority: route > token > ClientId > API key.
    /// </summary>
    private static Guid? ResolveTenantId(HttpContext context)
    {
        // 1. Route parameter (highest priority)
        if (context.Request.RouteValues.TryGetValue("tenantId", out var routeValue)
            && Guid.TryParse(routeValue?.ToString(), out var routeTenantId))
        {
            return routeTenantId;
        }

        // 2. Token claim
        var tokenTenantId = GetTenantIdFromToken(context);
        if (tokenTenantId.HasValue)
        {
            return tokenTenantId;
        }

        // 3. ClientId from token
        var clientIdClaim = context.User?.FindFirst("client_id")?.Value
                            ?? context.User?.FindFirst("azp")?.Value;
        if (!string.IsNullOrEmpty(clientIdClaim))
        {
            // In a full implementation, we'd look up the Application by ClientIdentifier
            // and return its TenantId. For now, we store it for controller-level resolution.
            context.Items["ClientId"] = clientIdClaim;
        }

        // 4. API key (validated by TokenValidationMiddleware, tenant already resolved)
        if (context.Items.TryGetValue("ApiKeyTenantId", out var apiKeyTenantObj)
            && apiKeyTenantObj is Guid apiKeyTenantId)
        {
            return apiKeyTenantId;
        }

        return null;
    }

    private static Guid? GetTenantIdFromToken(HttpContext context)
    {
        var tenantClaim = context.User?.FindFirst("tenant_id")?.Value
                          ?? context.User?.FindFirst("tid")?.Value;

        if (!string.IsNullOrEmpty(tenantClaim) && Guid.TryParse(tenantClaim, out var tenantId))
        {
            return tenantId;
        }

        return null;
    }

    private static bool IsSuperAdmin(HttpContext context)
    {
        var scopesClaim = context.User?.FindFirst("scopes")?.Value
                          ?? context.User?.FindFirst("scope")?.Value;

        if (string.IsNullOrEmpty(scopesClaim)) return false;

        return scopesClaim.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Any(s => s.Equals("SecurityServiceAdmin", StringComparison.OrdinalIgnoreCase)
                      || s.Equals("SecurityService.Admin", StringComparison.OrdinalIgnoreCase));
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
                new { code = "TenantResolutionFailed", message }
            },
            correlationId
        };

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/json";

        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        await context.Response.WriteAsync(JsonSerializer.Serialize(envelope, options));
    }
}
