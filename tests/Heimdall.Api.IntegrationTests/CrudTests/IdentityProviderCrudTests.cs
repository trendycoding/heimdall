using System.Net;
using System.Net.Http.Json;
using Heimdall.Api.Models;
using Heimdall.Application.Tenants.Commands.CreateTenant;
using Heimdall.Domain.Enums;
using Heimdall.Domain.ValueObjects;

namespace Heimdall.Api.IntegrationTests.CrudTests;

/// <summary>
/// Full CRUD lifecycle integration tests for IdentityProviderConfiguration entity.
/// create → read → update → deactivate → verify state
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class IdentityProviderCrudTests : IntegrationTestBase
{
    public IdentityProviderCrudTests(HeimdallWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task IdentityProvider_FullCrudLifecycle()
    {
        var tenantId = await CreateTestTenantAsync();

        // Create
        var createPayload = new
        {
            ApplicationId = (Guid?)null,
            ProviderType = ProviderType.EntraExternalId,
            Name = "Test IdP " + Guid.NewGuid().ToString("N")[..8],
            Issuer = "https://login.microsoftonline.com/test-tenant/v2.0",
            Audience = "api://test-app",
            ClientId = "test-client-id",
            JwksEndpoint = "https://login.microsoftonline.com/test-tenant/discovery/v2.0/keys",
            SamlMetadataUrl = (string?)null,
            AllowedAlgorithms = new[] { "RS256" },
            ClaimMappings = new[]
            {
                new { PlatformField = "Subject", SourceClaimName = "sub" },
                new { PlatformField = "Email", SourceClaimName = "email" }
            },
            ClockSkewToleranceSeconds = 300,
            Status = IdpStatus.Active
        };

        var createResponse = await Client.PostAsJsonAsync($"/api/tenants/{tenantId}/identity-providers", createPayload);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var createEnvelope = await createResponse.Content.ReadFromJsonAsync<ApiEnvelope<IdpResponse>>();
        Assert.NotNull(createEnvelope);
        Assert.True(createEnvelope.Success);
        Assert.NotNull(createEnvelope.Data);
        var providerId = createEnvelope.Data.ProviderId;
        Assert.NotEqual(Guid.Empty, providerId);

        // Read
        var getResponse = await Client.GetAsync($"/api/tenants/{tenantId}/identity-providers/{providerId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var getEnvelope = await getResponse.Content.ReadFromJsonAsync<ApiEnvelope<IdpResponse>>();
        Assert.NotNull(getEnvelope);
        Assert.True(getEnvelope.Success);
        Assert.NotNull(getEnvelope.Data);
        Assert.Equal(providerId, getEnvelope.Data.ProviderId);
        Assert.Equal(createPayload.Issuer, getEnvelope.Data.Issuer);

        // Update
        var updatePayload = new
        {
            ApplicationId = (Guid?)null,
            ProviderType = ProviderType.EntraExternalId,
            Name = createPayload.Name,
            Issuer = "https://login.microsoftonline.com/updated-tenant/v2.0",
            Audience = "api://updated-app",
            ClientId = "updated-client-id",
            JwksEndpoint = "https://login.microsoftonline.com/updated-tenant/discovery/v2.0/keys",
            SamlMetadataUrl = (string?)null,
            AllowedAlgorithms = new[] { "RS256", "RS384" },
            ClaimMappings = new[]
            {
                new { PlatformField = "Subject", SourceClaimName = "sub" },
                new { PlatformField = "Email", SourceClaimName = "preferred_username" }
            },
            ClockSkewToleranceSeconds = 120
        };

        var updateResponse = await Client.PutAsJsonAsync($"/api/tenants/{tenantId}/identity-providers/{providerId}", updatePayload);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        // Read after update
        var getAfterUpdate = await Client.GetAsync($"/api/tenants/{tenantId}/identity-providers/{providerId}");
        Assert.Equal(HttpStatusCode.OK, getAfterUpdate.StatusCode);

        var updatedEnvelope = await getAfterUpdate.Content.ReadFromJsonAsync<ApiEnvelope<IdpResponse>>();
        Assert.NotNull(updatedEnvelope);
        Assert.True(updatedEnvelope.Success);
        Assert.NotNull(updatedEnvelope.Data);
        Assert.Equal(updatePayload.Issuer, updatedEnvelope.Data.Issuer);
        Assert.Equal(updatePayload.Audience, updatedEnvelope.Data.Audience);

        // Deactivate
        var deleteResponse = await Client.DeleteAsync($"/api/tenants/{tenantId}/identity-providers/{providerId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // Verify deactivated
        var getAfterDelete = await Client.GetAsync($"/api/tenants/{tenantId}/identity-providers/{providerId}");
        if (getAfterDelete.StatusCode == HttpStatusCode.OK)
        {
            var deactivatedEnvelope = await getAfterDelete.Content.ReadFromJsonAsync<ApiEnvelope<IdpResponse>>();
            Assert.NotNull(deactivatedEnvelope);
            Assert.NotNull(deactivatedEnvelope.Data);
            Assert.Equal("Inactive", deactivatedEnvelope.Data.Status);
        }
        else
        {
            Assert.Equal(HttpStatusCode.NotFound, getAfterDelete.StatusCode);
        }
    }

    private async Task<Guid> CreateTestTenantAsync()
    {
        var slug = "idp-test-" + Guid.NewGuid().ToString("N")[..8];
        var payload = new CreateTenantCommand("IdP Test Tenant", slug, PrimaryIdentityMode.EntraExternalId);
        var response = await Client.PostAsJsonAsync("/api/tenants", payload);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<CreateTenantResult>>();
        return envelope!.Data!.TenantId;
    }

    private sealed record IdpResponse(
        Guid ProviderId,
        string Name,
        string Issuer,
        string? Audience,
        string? Status);
}
