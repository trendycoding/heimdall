using System.Net;
using System.Net.Http.Json;
using Heimdall.Api.Models;
using Heimdall.Application.Tenants.Commands.CreateTenant;
using Heimdall.Domain.Enums;

namespace Heimdall.Api.IntegrationTests.CrudTests;

/// <summary>
/// Full CRUD lifecycle integration tests for FunctionalArea entity.
/// create → read → update → deactivate → verify state
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class FunctionalAreaCrudTests : IntegrationTestBase
{
    public FunctionalAreaCrudTests(HeimdallWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task FunctionalArea_FullCrudLifecycle()
    {
        var (tenantId, appId) = await CreateTestTenantAndAppAsync();

        // Create
        var code = "FA_TEST_" + Guid.NewGuid().ToString("N")[..6].ToUpper();
        var createPayload = new
        {
            FunctionalAreaCode = code,
            Name = "Test Functional Area",
            Description = "A test functional area for integration testing"
        };

        var createResponse = await Client.PostAsJsonAsync(
            $"/api/tenants/{tenantId}/applications/{appId}/functional-areas", createPayload);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var createEnvelope = await createResponse.Content.ReadFromJsonAsync<ApiEnvelope<FunctionalAreaResponse>>();
        Assert.NotNull(createEnvelope);
        Assert.True(createEnvelope.Success);
        Assert.NotNull(createEnvelope.Data);
        var faId = createEnvelope.Data.Id;
        Assert.NotEqual(Guid.Empty, faId);

        // Read
        var getResponse = await Client.GetAsync(
            $"/api/tenants/{tenantId}/applications/{appId}/functional-areas/{faId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var getEnvelope = await getResponse.Content.ReadFromJsonAsync<ApiEnvelope<FunctionalAreaResponse>>();
        Assert.NotNull(getEnvelope);
        Assert.True(getEnvelope.Success);
        Assert.NotNull(getEnvelope.Data);
        Assert.Equal(faId, getEnvelope.Data.Id);
        Assert.Equal(code, getEnvelope.Data.FunctionalAreaCode);
        Assert.Equal(createPayload.Name, getEnvelope.Data.Name);

        // Update
        var updatePayload = new
        {
            Name = "Updated Functional Area",
            Description = "Updated description"
        };

        var updateResponse = await Client.PutAsJsonAsync(
            $"/api/tenants/{tenantId}/applications/{appId}/functional-areas/{faId}", updatePayload);
        Assert.Equal(HttpStatusCode.NoContent, updateResponse.StatusCode);

        // Read after update
        var getAfterUpdate = await Client.GetAsync(
            $"/api/tenants/{tenantId}/applications/{appId}/functional-areas/{faId}");
        Assert.Equal(HttpStatusCode.OK, getAfterUpdate.StatusCode);

        var updatedEnvelope = await getAfterUpdate.Content.ReadFromJsonAsync<ApiEnvelope<FunctionalAreaResponse>>();
        Assert.NotNull(updatedEnvelope);
        Assert.True(updatedEnvelope.Success);
        Assert.NotNull(updatedEnvelope.Data);
        Assert.Equal("Updated Functional Area", updatedEnvelope.Data.Name);
        Assert.Equal("Updated description", updatedEnvelope.Data.Description);
        // Code should remain unchanged
        Assert.Equal(code, updatedEnvelope.Data.FunctionalAreaCode);

        // Deactivate
        var deleteResponse = await Client.DeleteAsync(
            $"/api/tenants/{tenantId}/applications/{appId}/functional-areas/{faId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // Verify deactivated
        var getAfterDelete = await Client.GetAsync(
            $"/api/tenants/{tenantId}/applications/{appId}/functional-areas/{faId}");
        if (getAfterDelete.StatusCode == HttpStatusCode.OK)
        {
            var deactivatedEnvelope = await getAfterDelete.Content.ReadFromJsonAsync<ApiEnvelope<FunctionalAreaResponse>>();
            Assert.NotNull(deactivatedEnvelope);
            Assert.NotNull(deactivatedEnvelope.Data);
            Assert.False(deactivatedEnvelope.Data.IsActive);
        }
        else
        {
            Assert.Equal(HttpStatusCode.NotFound, getAfterDelete.StatusCode);
        }
    }

    private async Task<(Guid TenantId, Guid AppId)> CreateTestTenantAndAppAsync()
    {
        var slug = "fa-test-" + Guid.NewGuid().ToString("N")[..8];
        var tenantPayload = new CreateTenantCommand("FA Test Tenant", slug, PrimaryIdentityMode.EntraExternalId);
        var tenantResponse = await Client.PostAsJsonAsync("/api/tenants", tenantPayload);
        tenantResponse.EnsureSuccessStatusCode();
        var tenantEnvelope = await tenantResponse.Content.ReadFromJsonAsync<ApiEnvelope<CreateTenantResult>>();
        var tenantId = tenantEnvelope!.Data!.TenantId;

        var appPayload = new
        {
            Name = "FA Test App",
            ClientIdentifier = "fa-test-client-" + Guid.NewGuid().ToString("N")[..8],
            Description = "Test app for functional areas"
        };
        var appResponse = await Client.PostAsJsonAsync($"/api/tenants/{tenantId}/applications", appPayload);
        appResponse.EnsureSuccessStatusCode();
        var appEnvelope = await appResponse.Content.ReadFromJsonAsync<ApiEnvelope<AppResponse>>();
        return (tenantId, appEnvelope!.Data!.ApplicationId);
    }

    private sealed record FunctionalAreaResponse(Guid Id, string FunctionalAreaCode, string Name, string? Description, bool IsActive);
    private sealed record AppResponse(Guid ApplicationId);
}
