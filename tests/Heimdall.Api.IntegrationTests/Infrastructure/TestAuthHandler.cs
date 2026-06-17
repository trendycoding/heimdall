using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Heimdall.Api.IntegrationTests.Infrastructure;

/// <summary>
/// A test authentication handler that simulates authenticated requests
/// with configurable tenants, scopes, and claims. Used in integration tests
/// to bypass real token validation while still populating the ClaimsPrincipal.
/// </summary>
public class TestAuthHandler : AuthenticationHandler<TestAuthHandlerOptions>
{
    public const string AuthenticationScheme = "TestScheme";

    public TestAuthHandler(
        IOptionsMonitor<TestAuthHandlerOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Allow unauthenticated requests to pass through (for testing 401 scenarios)
        if (Context.Request.Headers.ContainsKey("X-Test-Anonymous"))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new List<Claim>
        {
            new("sub", Options.SubjectId),
            new("email", Options.Email),
        };

        if (Options.TenantId != Guid.Empty)
        {
            claims.Add(new Claim("tenant_id", Options.TenantId.ToString()));
        }

        if (Options.ApplicationId.HasValue)
        {
            claims.Add(new Claim("application_id", Options.ApplicationId.Value.ToString()));
        }

        if (Options.UserProfileId.HasValue)
        {
            claims.Add(new Claim("user_profile_id", Options.UserProfileId.Value.ToString()));
        }

        foreach (var scope in Options.Scopes)
        {
            claims.Add(new Claim("scopes", scope));
        }

        foreach (var (key, value) in Options.AdditionalClaims)
        {
            claims.Add(new Claim(key, value));
        }

        var identity = new ClaimsIdentity(claims, AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, AuthenticationScheme);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

/// <summary>
/// Configuration options for the test authentication handler.
/// </summary>
public class TestAuthHandlerOptions : AuthenticationSchemeOptions
{
    /// <summary>
    /// The subject identifier for the authenticated user.
    /// </summary>
    public string SubjectId { get; set; } = "test-subject-id";

    /// <summary>
    /// The email address for the authenticated user.
    /// </summary>
    public string Email { get; set; } = "test@heimdall.dev";

    /// <summary>
    /// The tenant ID the authenticated user belongs to.
    /// </summary>
    public Guid TenantId { get; set; } = Guid.Empty;

    /// <summary>
    /// The application ID context for the request (optional).
    /// </summary>
    public Guid? ApplicationId { get; set; }

    /// <summary>
    /// The user profile ID for the authenticated user (optional).
    /// </summary>
    public Guid? UserProfileId { get; set; }

    /// <summary>
    /// Admin scopes assigned to the authenticated user.
    /// </summary>
    public List<string> Scopes { get; set; } = new() { "SecurityService.Admin" };

    /// <summary>
    /// Additional custom claims to include in the identity.
    /// </summary>
    public Dictionary<string, string> AdditionalClaims { get; set; } = new();
}
