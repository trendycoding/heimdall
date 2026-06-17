using FsCheck;
using FsCheck.Xunit;
using Heimdall.Api.Middleware;
using Heimdall.Domain.Enums;
using Heimdall.Domain.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Heimdall.Api.IntegrationTests.Properties;

/// <summary>
/// Property 23: Administrative Scope Enforcement
/// Generate callers with various scope combinations and target endpoints;
/// assert access granted iff caller holds required scope OR SecurityService.Admin.
///
/// **Validates: Requirements 20.2, 20.3**
/// </summary>
public class AdminAuthorization_ScopeTests
{
    /// <summary>
    /// Endpoint definitions with their required scopes.
    /// These mirror the exact logic in AdminAuthorizationMiddleware.GetRequiredScope(),
    /// which uses path.Contains() matching in order. Paths containing "/applications"
    /// always resolve to ApplicationAdmin regardless of sub-resources because
    /// the "/applications" check comes first in the middleware's if-chain.
    /// </summary>
    private static readonly (string Path, string Method, string RequiredScope)[] ProtectedEndpoints =
    [
        // Tenant creation (matches: Contains("/tenants") && !Contains("/tenants/"))
        ("/api/tenants", "POST", nameof(AdminScope.TenantAdmin)),
        // Application management (matches: Contains("/tenants/") then Contains("/applications"))
        ("/api/tenants/00000000-0000-0000-0000-000000000001/applications", "POST", nameof(AdminScope.ApplicationAdmin)),
        ("/api/tenants/00000000-0000-0000-0000-000000000001/applications", "GET", nameof(AdminScope.ApplicationAdmin)),
        // Sub-resources under /applications also match "/applications" first → ApplicationAdmin
        ("/api/tenants/00000000-0000-0000-0000-000000000001/applications/00000000-0000-0000-0000-000000000002/functional-areas", "POST", nameof(AdminScope.ApplicationAdmin)),
        ("/api/tenants/00000000-0000-0000-0000-000000000001/applications/00000000-0000-0000-0000-000000000002/permission-types", "GET", nameof(AdminScope.ApplicationAdmin)),
        ("/api/tenants/00000000-0000-0000-0000-000000000001/applications/00000000-0000-0000-0000-000000000002/groups", "POST", nameof(AdminScope.ApplicationAdmin)),
        // Identity providers (matches: Contains("/tenants/") then Contains("/identity-providers"))
        ("/api/tenants/00000000-0000-0000-0000-000000000001/identity-providers", "POST", nameof(AdminScope.IdentityProviderAdmin)),
        ("/api/tenants/00000000-0000-0000-0000-000000000001/identity-providers", "GET", nameof(AdminScope.IdentityProviderAdmin)),
        // Users (matches: Contains("/tenants/") then Contains("/users"))
        ("/api/tenants/00000000-0000-0000-0000-000000000001/users", "POST", nameof(AdminScope.TenantAdmin)),
        ("/api/tenants/00000000-0000-0000-0000-000000000001/users", "GET", nameof(AdminScope.TenantAdmin)),
        // Direct tenant-level paths without sub-resources matching earlier checks
        // These match the fallback: Contains("/tenants/") + PUT/DELETE → TenantAdmin
        ("/api/tenants/00000000-0000-0000-0000-000000000001", "PUT", nameof(AdminScope.TenantAdmin)),
        ("/api/tenants/00000000-0000-0000-0000-000000000001", "DELETE", nameof(AdminScope.TenantAdmin)),
        // Audit/API logs (matches: Contains("/tenants/") then Contains("/audit-logs") or "/api-call-logs")
        ("/api/tenants/00000000-0000-0000-0000-000000000001/audit-logs", "GET", nameof(AdminScope.Auditor)),
        ("/api/tenants/00000000-0000-0000-0000-000000000001/api-call-logs", "GET", nameof(AdminScope.Auditor)),
    ];

    /// <summary>
    /// All non-SecurityServiceAdmin scopes that can be assigned to callers.
    /// </summary>
    private static readonly string[] AllNonAdminScopes =
    [
        nameof(AdminScope.TenantAdmin),
        nameof(AdminScope.ApplicationAdmin),
        nameof(AdminScope.IdentityProviderAdmin),
        nameof(AdminScope.PermissionManager),
        nameof(AdminScope.TemplateManager),
        nameof(AdminScope.AccessDetailManager),
        nameof(AdminScope.Auditor),
        nameof(AdminScope.ReadOnly),
    ];

    /// <summary>
    /// Property: When a caller holds the correct required scope for an endpoint,
    /// the middleware grants access (invokes next delegate).
    /// </summary>
    [Property(MaxTest = 200)]
    public bool CallerWithCorrectScope_IsGrantedAccess(NonNegativeInt endpointIndexRaw)
    {
        var endpointIndex = endpointIndexRaw.Get % ProtectedEndpoints.Length;
        var (path, method, requiredScope) = ProtectedEndpoints[endpointIndex];

        return RunMiddlewareTestAsync(
            path, method,
            callerScopes: new[] { requiredScope },
            isSuperAdmin: false,
            expectGranted: true
        ).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Property: When a caller lacks the required scope and is not SecurityServiceAdmin,
    /// the middleware returns 403.
    /// </summary>
    [Property(MaxTest = 200)]
    public bool CallerWithoutRequiredScope_IsDeniedAccess(NonNegativeInt endpointIndexRaw, NonNegativeInt scopeSubsetSeedRaw)
    {
        var endpointIndex = endpointIndexRaw.Get % ProtectedEndpoints.Length;
        var (path, method, requiredScope) = ProtectedEndpoints[endpointIndex];

        // Generate a random subset of scopes that excludes the required scope
        var scopeSubsetSeed = scopeSubsetSeedRaw.Get;
        var wrongScopes = AllNonAdminScopes
            .Where(s => !s.Equals(requiredScope, StringComparison.OrdinalIgnoreCase))
            .Where((_, i) => (scopeSubsetSeed >> i & 1) == 1)
            .ToList();

        return RunMiddlewareTestAsync(
            path, method,
            callerScopes: wrongScopes,
            isSuperAdmin: false,
            expectGranted: false
        ).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Property: SecurityServiceAdmin scope always grants access regardless of required scope.
    /// </summary>
    [Property(MaxTest = 200)]
    public bool SecurityServiceAdmin_AlwaysGrantedAccess(NonNegativeInt endpointIndexRaw, NonNegativeInt extraScopesSeedRaw)
    {
        var endpointIndex = endpointIndexRaw.Get % ProtectedEndpoints.Length;
        var (path, method, _) = ProtectedEndpoints[endpointIndex];

        // Generate random extra scopes alongside SecurityServiceAdmin
        var extraScopesSeed = extraScopesSeedRaw.Get;
        var scopes = AllNonAdminScopes
            .Where((_, i) => (extraScopesSeed >> i & 1) == 1)
            .Append(nameof(AdminScope.SecurityServiceAdmin))
            .ToList();

        return RunMiddlewareTestAsync(
            path, method,
            callerScopes: scopes,
            isSuperAdmin: true,
            expectGranted: true
        ).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Property: Callers with multiple scopes (including the required one) are granted access.
    /// </summary>
    [Property(MaxTest = 100)]
    public bool CallerWithMultipleScopesIncludingRequired_IsGrantedAccess(
        NonNegativeInt endpointIndexRaw, NonNegativeInt extraScopesSeedRaw)
    {
        var endpointIndex = endpointIndexRaw.Get % ProtectedEndpoints.Length;
        var (path, method, requiredScope) = ProtectedEndpoints[endpointIndex];

        // Generate random extra scopes and always include the required scope
        var extraScopesSeed = extraScopesSeedRaw.Get;
        var scopes = AllNonAdminScopes
            .Where((_, i) => (extraScopesSeed >> i & 1) == 1)
            .Append(requiredScope)
            .Distinct()
            .ToList();

        return RunMiddlewareTestAsync(
            path, method,
            callerScopes: scopes,
            isSuperAdmin: false,
            expectGranted: true
        ).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Property: Callers with empty scopes and non-SuperAdmin are denied on all protected endpoints.
    /// </summary>
    [Property(MaxTest = 100)]
    public bool CallerWithNoScopes_IsDeniedOnProtectedEndpoints(NonNegativeInt endpointIndexRaw)
    {
        var endpointIndex = endpointIndexRaw.Get % ProtectedEndpoints.Length;
        var (path, method, _) = ProtectedEndpoints[endpointIndex];

        return RunMiddlewareTestAsync(
            path, method,
            callerScopes: Array.Empty<string>(),
            isSuperAdmin: false,
            expectGranted: false
        ).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Executes the AdminAuthorizationMiddleware with the given parameters and asserts the expected outcome.
    /// </summary>
    private static async Task<bool> RunMiddlewareTestAsync(
        string path,
        string method,
        IEnumerable<string> callerScopes,
        bool isSuperAdmin,
        bool expectGranted)
    {
        var nextInvoked = false;

        // Mock ITenantContext
        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(Guid.NewGuid());
        tenantContext.AdminScopes.Returns(callerScopes.ToList().AsReadOnly());
        tenantContext.IsSuperAdmin.Returns(isSuperAdmin);
        tenantContext.ActorSubjectId.Returns("test-subject");
        tenantContext.ActorEmail.Returns("test@example.com");

        // Create mock logger
        var logger = Substitute.For<ILogger<AdminAuthorizationMiddleware>>();

        // Create the middleware with a next delegate that sets our flag
        RequestDelegate next = _ =>
        {
            nextInvoked = true;
            return Task.CompletedTask;
        };

        var middleware = new AdminAuthorizationMiddleware(next, logger);

        // Create HttpContext
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = path;
        httpContext.Request.Method = method;

        // Invoke the middleware
        await middleware.InvokeAsync(httpContext, tenantContext);

        if (expectGranted)
        {
            // If access should be granted, next must be invoked and status should not be 403
            return nextInvoked && httpContext.Response.StatusCode != StatusCodes.Status403Forbidden;
        }
        else
        {
            // If access should be denied, next must NOT be invoked and status should be 403
            return !nextInvoked && httpContext.Response.StatusCode == StatusCodes.Status403Forbidden;
        }
    }
}
