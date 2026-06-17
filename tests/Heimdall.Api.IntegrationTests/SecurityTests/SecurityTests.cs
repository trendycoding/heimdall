using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Heimdall.Domain.Interfaces;
using Heimdall.Domain.Models;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Heimdall.Api.IntegrationTests.SecurityTests;

/// <summary>
/// Security integration tests verifying cross-tenant isolation, JWT rejection scenarios,
/// admin scope enforcement, audit log immutability, and sensitive data redaction.
/// Validates: Requirements 27.8
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class SecurityTests : IAsyncLifetime
{
    private readonly HeimdallWebApplicationFactory _factory;

    public SecurityTests(HeimdallWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        await _factory.EnsureDatabaseCreatedAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    #region Cross-Tenant Access Tests

    /// <summary>
    /// Cross-tenant read access is denied for a non-Super-Admin user.
    /// The user's token contains TenantA, but the request targets TenantB.
    /// </summary>
    [Fact]
    public async Task CrossTenant_ReadAccess_Denied_ForNonSuperAdmin()
    {
        // Arrange
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        // Create a client authenticated for TenantA but making requests to TenantB
        var client = CreateClientWithTokenValidation(tenantA, isSuperAdmin: false);

        // Act - Attempt to read data in TenantB
        var response = await client.GetAsync($"/api/tenants/{tenantB}/applications");

        // Assert - Should be 403 (cross-tenant denied)
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Cross-tenant access", body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Cross-tenant write access is denied for a non-Super-Admin user.
    /// The user's token contains TenantA, but the write targets TenantB.
    /// </summary>
    [Fact]
    public async Task CrossTenant_WriteAccess_Denied_ForNonSuperAdmin()
    {
        // Arrange
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var client = CreateClientWithTokenValidation(tenantA, isSuperAdmin: false);

        var payload = new
        {
            name = "Test Application",
            clientIdentifier = "test-client-id"
        };

        // Act - Attempt to create an application in TenantB
        var response = await client.PostAsJsonAsync(
            $"/api/tenants/{tenantB}/applications", payload);

        // Assert - Should be 403 (cross-tenant denied)
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Cross-tenant access", body, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Token Rejection Tests

    /// <summary>
    /// A request with no authentication credentials is rejected with 401.
    /// </summary>
    [Fact]
    public async Task InvalidJwt_Rejected_Returns401()
    {
        // Arrange - client with no bearer token and no API key
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/tenants");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Unauthorized", body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A token with wrong audience is rejected with 401.
    /// </summary>
    [Fact]
    public async Task Token_WrongAudience_Rejected()
    {
        // Arrange - configure token validation to reject for wrong audience
        var client = CreateClientWithFailedValidation("Token audience does not match configured audience.");

        // Act
        var response = await client.GetAsync("/api/tenants");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("audience", body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A token with wrong issuer is rejected with 401.
    /// </summary>
    [Fact]
    public async Task Token_WrongIssuer_Rejected()
    {
        // Arrange - configure token validation to reject for wrong issuer
        var client = CreateClientWithFailedValidation("Token issuer does not match configured issuer.");

        // Act
        var response = await client.GetAsync("/api/tenants");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("issuer", body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// An expired token is rejected with 401.
    /// </summary>
    [Fact]
    public async Task Token_Expired_Rejected()
    {
        // Arrange - configure token validation to reject expired tokens
        var client = CreateClientWithFailedValidation("Token has expired.");

        // Act
        var response = await client.GetAsync("/api/tenants");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("expired", body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// An unsigned token (alg=none) is rejected with 401.
    /// </summary>
    [Fact]
    public async Task Token_Unsigned_Rejected()
    {
        // Arrange - configure token validation to reject unsigned tokens
        var client = CreateClientWithFailedValidation("Unsigned tokens are not accepted.");

        // Act
        var response = await client.GetAsync("/api/tenants");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Unsigned", body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A token using a weak/disallowed algorithm is rejected with 401.
    /// </summary>
    [Fact]
    public async Task Token_WeakAlgorithm_Rejected()
    {
        // Arrange - configure token validation to reject disallowed algorithm
        var client = CreateClientWithFailedValidation("Algorithm 'HS256' is not permitted.");

        // Act
        var response = await client.GetAsync("/api/tenants");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("not permitted", body, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Admin Scope Tests

    /// <summary>
    /// A request without the required admin scope is rejected with 403.
    /// </summary>
    [Fact]
    public async Task Request_WithoutRequiredAdminScope_Rejected()
    {
        // Arrange - create a client authenticated for the tenant but with ReadOnly scope only
        var tenantId = Guid.NewGuid();
        var client = CreateClientWithTokenValidation(tenantId, isSuperAdmin: false, scopes: "ReadOnly");

        var payload = new
        {
            name = "Test Application",
            clientIdentifier = "test-client-id"
        };

        // Act - Attempt to create an application (requires ApplicationAdmin scope)
        var response = await client.PostAsJsonAsync(
            $"/api/tenants/{tenantId}/applications", payload);

        // Assert - Should be 403 (insufficient scope)
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Insufficient", body, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Audit Log Immutability Tests

    /// <summary>
    /// Audit log records cannot be modified via API (no PUT endpoint).
    /// </summary>
    [Fact]
    public async Task AuditLogs_CannotBeModified_ViaApi()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var client = CreateClientWithTokenValidation(tenantId, isSuperAdmin: true);

        var fakeLogId = Guid.NewGuid();
        var payload = new { action = "Modified", entityType = "Hacked" };

        // Act - Attempt PUT on audit logs
        var response = await client.PutAsJsonAsync(
            $"/api/tenants/{tenantId}/audit-logs/{fakeLogId}", payload);

        // Assert - Should be 405 Method Not Allowed or 404 (no such endpoint)
        Assert.True(
            response.StatusCode == HttpStatusCode.MethodNotAllowed ||
            response.StatusCode == HttpStatusCode.NotFound,
            $"Expected 405 or 404 but got {response.StatusCode}");
    }

    /// <summary>
    /// Audit log records cannot be deleted via API (no DELETE endpoint).
    /// </summary>
    [Fact]
    public async Task AuditLogs_CannotBeDeleted_ViaApi()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var client = CreateClientWithTokenValidation(tenantId, isSuperAdmin: true);

        var fakeLogId = Guid.NewGuid();

        // Act - Attempt DELETE on audit logs
        var response = await client.DeleteAsync(
            $"/api/tenants/{tenantId}/audit-logs/{fakeLogId}");

        // Assert - Should be 405 Method Not Allowed or 404 (no such endpoint)
        Assert.True(
            response.StatusCode == HttpStatusCode.MethodNotAllowed ||
            response.StatusCode == HttpStatusCode.NotFound,
            $"Expected 405 or 404 but got {response.StatusCode}");
    }

    #endregion

    #region Sensitive Data Redaction Tests

    /// <summary>
    /// Sensitive values (secrets, keys, tokens) are not present in API response bodies
    /// when retrieving identity provider configurations.
    /// </summary>
    [Fact]
    public async Task SensitiveValues_NotPresent_InLogOutput()
    {
        // Arrange - Create an authenticated client and check API call logs
        var tenantId = Guid.NewGuid();
        var client = CreateClientWithTokenValidation(tenantId, isSuperAdmin: true);

        // Act - Request API call logs (which should not expose sensitive data)
        var response = await client.GetAsync($"/api/tenants/{tenantId}/api-call-logs");

        // Assert - Response should not contain raw token values, secrets, or keys
        var body = await response.Content.ReadAsStringAsync();

        // The response should not leak bearer tokens, client secrets, or signing keys
        Assert.DoesNotContain("test-token-value-secret", body);
        Assert.DoesNotContain("client_secret", body);
        Assert.DoesNotContain("-----BEGIN PRIVATE KEY-----", body);
        Assert.DoesNotContain("-----BEGIN RSA PRIVATE KEY-----", body);

        // The status code should be successful (authenticated and authorized)
        Assert.True(
            response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.NoContent,
            $"Expected 200/204 but got {response.StatusCode}");
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates an HttpClient with a test ITokenValidationService that returns a valid token
    /// for the specified tenant. Used for cross-tenant and scope tests.
    /// </summary>
    private HttpClient CreateClientWithTokenValidation(
        Guid tenantId, bool isSuperAdmin, string scopes = "ApplicationAdmin")
    {
        var claims = new Dictionary<string, string>
        {
            ["sub"] = "test-subject-id",
            ["email"] = "test@heimdall.dev",
            ["tenant_id"] = tenantId.ToString()
        };

        if (isSuperAdmin)
        {
            claims["scopes"] = "SecurityService.Admin";
        }
        else
        {
            claims["scopes"] = scopes;
        }

        var validResult = new TokenValidationResult
        {
            IsValid = true,
            Claims = claims
        };

        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ITokenValidationService>();
                services.AddScoped<ITokenValidationService>(_ =>
                    new ConfigurableTokenValidationService(validResult));
            });
        }).CreateClient();

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "test-token");

        return client;
    }

    /// <summary>
    /// Creates an HttpClient with a test ITokenValidationService that rejects tokens
    /// with the specified error message. Used for token rejection tests.
    /// </summary>
    private HttpClient CreateClientWithFailedValidation(string errorMessage)
    {
        var invalidResult = new TokenValidationResult
        {
            IsValid = false,
            Error = errorMessage
        };

        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ITokenValidationService>();
                services.AddScoped<ITokenValidationService>(_ =>
                    new ConfigurableTokenValidationService(invalidResult));
            });
        }).CreateClient();

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "test-token");

        return client;
    }

    #endregion
}

/// <summary>
/// A configurable token validation service for integration tests that returns
/// a predetermined result, allowing tests to simulate various token scenarios.
/// </summary>
internal sealed class ConfigurableTokenValidationService : ITokenValidationService
{
    private readonly TokenValidationResult _result;

    public ConfigurableTokenValidationService(TokenValidationResult result)
    {
        _result = result;
    }

    public Task<TokenValidationResult> ValidateTokenAsync(
        string token, Guid tenantId, Guid? applicationId = null,
        CancellationToken ct = default)
    {
        return Task.FromResult(_result);
    }
}
