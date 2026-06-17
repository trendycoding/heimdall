using System.Net;
using System.Net.Http.Json;
using Heimdall.Api.Models;
using Heimdall.Application.Tenants.Commands.CreateTenant;
using Heimdall.Domain.Enums;

namespace Heimdall.Api.IntegrationTests.CrudTests;

/// <summary>
/// Full CRUD lifecycle integration tests for Application entity.
/// create → read → update → deactivate → verify state
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class ApplicationCrudTests : IntegrationTestBase
{
    public ApplicationCrudTests(HeimdallWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Application_FullCrudLifecycle()
    {
        // First create a tenant as parent
        var tenantId = await CreateTestTenantAsync();

        // Create application
        var createPayload = new
        {
            Name = "Test Application",
            ClientIdentifier = "test-client-" + Guid.NewGuid().ToString("N")[..8],
            Description = "Integration test application",
            AllowedRedirectUris = new[] { "https://localhost:5001/callback" },
            AllowedOrigins = new[] { "https://localhost:5001" }
        };

        var createResponse = await Client.PostAsJsonAsync($"/api/tenants/{tenantId}/applications", createPayload);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var createEnvelope = await createResponse.Content.ReadFromJsonAsync<ApiEnvelope<ApplicationResponse>>();
        Assert.NotNull(createEnvelope);
        Assert.True(createEnvelope.Success);
        Assert.NotNull(createEnvelope.Data);
        var appId = createEnvelope.Data.ApplicationId;
        Assert.NotEqual(Guid.Empty, appId);
        Assert.Equal(createPayload.Name, createEnvelope.Data.Name);

        // Read
        var getResponse = await Client.GetAsync($"/api/tenants/{tenantId}/applications/{appId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var getEnvelope = await getResponse.Content.ReadFromJsonAsync<ApiEnvelope<ApplicationResponse>>();
        Assert.NotNull(getEnvelope);
        Assert.True(getEnvelope.Success);
        Assert.NotNull(getEnvelope.Data);
        Assert.Equal(appId, getEnvelope.Data.ApplicationId);
        Assert.Equal(createPayload.Name, getEnvelope.Data.Name);
        Assert.Equal(createPayload.ClientIdentifier, getEnvelope.Data.ClientIdentifier);

        // Update
        var updatePayload = new
        {
            Name = "Updated Application Name",
            Description = "Updated description",
            AllowedRedirectUris = new[] { "https://localhost:5001/callback", "https://app.example.com/callback" },
            AllowedOrigins = new[] { "https://localhost:5001", "https://app.example.com" }
        };

        var updateResponse = await Client.PutAsJsonAsync($"/api/tenants/{tenantId}/applications/{appId}", updatePayload);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        // Read after update
        var getAfterUpdate = await Client.GetAsync($"/api/tenants/{tenantId}/applications/{appId}");
        Assert.Equal(HttpStatusCode.OK, getAfterUpdate.StatusCode);

        var updatedEnvelope = await getAfterUpdate.Content.ReadFromJsonAsync<ApiEnvelope<ApplicationResponse>>();
        Assert.NotNull(updatedEnvelope);
        Assert.True(updatedEnvelope.Success);
        Assert.NotNull(updatedEnvelope.Data);
        Assert.Equal("Updated Application Name", updatedEnvelope.Data.Name);
        Assert.Equal("Updated description", updatedEnvelope.Data.Description);

        // Deactivate
        var deleteResponse = await Client.DeleteAsync($"/api/tenants/{tenantId}/applications/{appId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // Verify state after deactivation
        var getAfterDelete = await Client.GetAsync($"/api/tenants/{tenantId}/applications/{appId}");
        if (getAfterDelete.StatusCode == HttpStatusCode.OK)
        {
            var deactivatedEnvelope = await getAfterDelete.Content.ReadFromJsonAsync<ApiEnvelope<ApplicationResponse>>();
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
        var slug = "app-test-" + Guid.NewGuid().ToString("N")[..8];
        var payload = new CreateTenantCommand("App Test Tenant", slug, PrimaryIdentityMode.EntraExternalId);
        var response = await Client.PostAsJsonAsync("/api/tenants", payload);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<CreateTenantResult>>();
        return envelope!.Data!.TenantId;
    }

    private sealed record ApplicationResponse(
        Guid ApplicationId,
        string Name,
        string ClientIdentifier,
        string? Description,
        string Status,
        List<string>? AllowedRedirectUris,
        List<string>? AllowedOrigins);
}
