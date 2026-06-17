using System.Net;
using System.Net.Http.Json;
using Heimdall.Api.Models;
using Heimdall.Application.Tenants.Commands.CreateTenant;
using Heimdall.Domain.Enums;

namespace Heimdall.Api.IntegrationTests.CrudTests;

/// <summary>
/// Full CRUD lifecycle integration tests for Tenant entity.
/// create → read → update → deactivate → verify state
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class TenantCrudTests : IntegrationTestBase
{
    public TenantCrudTests(HeimdallWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Tenant_FullCrudLifecycle()
    {
        // Create
        var createPayload = new CreateTenantCommand(
            "Integration Test Tenant",
            "int-test-tenant-" + Guid.NewGuid().ToString("N")[..8],
            PrimaryIdentityMode.EntraExternalId);

        var createResponse = await Client.PostAsJsonAsync("/api/tenants", createPayload);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var createEnvelope = await createResponse.Content.ReadFromJsonAsync<ApiEnvelope<CreateTenantResult>>();
        Assert.NotNull(createEnvelope);
        Assert.True(createEnvelope.Success);
        Assert.NotNull(createEnvelope.Data);
        var tenantId = createEnvelope.Data.TenantId;
        Assert.NotEqual(Guid.Empty, tenantId);
        Assert.Equal(createPayload.Name, createEnvelope.Data.Name);
        Assert.Equal(createPayload.Slug, createEnvelope.Data.Slug);
        Assert.Equal(TenantStatus.Active, createEnvelope.Data.Status);

        // Read
        var getResponse = await Client.GetAsync($"/api/tenants/{tenantId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var getEnvelope = await getResponse.Content.ReadFromJsonAsync<ApiEnvelope<CreateTenantResult>>();
        Assert.NotNull(getEnvelope);
        Assert.True(getEnvelope.Success);
        Assert.NotNull(getEnvelope.Data);
        Assert.Equal(tenantId, getEnvelope.Data.TenantId);
        Assert.Equal(createPayload.Name, getEnvelope.Data.Name);

        // Update
        var newSlug = "updated-slug-" + Guid.NewGuid().ToString("N")[..8];
        var updatePayload = new
        {
            Name = "Updated Tenant Name",
            Slug = newSlug,
            PrimaryIdentityMode = PrimaryIdentityMode.AzureAdB2C
        };

        var updateResponse = await Client.PutAsJsonAsync($"/api/tenants/{tenantId}", updatePayload);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        // Read after update to verify
        var getAfterUpdate = await Client.GetAsync($"/api/tenants/{tenantId}");
        Assert.Equal(HttpStatusCode.OK, getAfterUpdate.StatusCode);

        var updatedEnvelope = await getAfterUpdate.Content.ReadFromJsonAsync<ApiEnvelope<CreateTenantResult>>();
        Assert.NotNull(updatedEnvelope);
        Assert.True(updatedEnvelope.Success);
        Assert.NotNull(updatedEnvelope.Data);
        Assert.Equal("Updated Tenant Name", updatedEnvelope.Data.Name);
        Assert.Equal(newSlug, updatedEnvelope.Data.Slug);
        Assert.Equal(PrimaryIdentityMode.AzureAdB2C, updatedEnvelope.Data.PrimaryIdentityMode);

        // Deactivate
        var deleteResponse = await Client.DeleteAsync($"/api/tenants/{tenantId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // Verify deactivated state - should be not found or inactive
        var getAfterDelete = await Client.GetAsync($"/api/tenants/{tenantId}");
        // After deactivation, the entity should still be retrievable but with Inactive status
        // or return 404 depending on implementation
        if (getAfterDelete.StatusCode == HttpStatusCode.OK)
        {
            var deactivatedEnvelope = await getAfterDelete.Content.ReadFromJsonAsync<ApiEnvelope<CreateTenantResult>>();
            Assert.NotNull(deactivatedEnvelope);
            Assert.True(deactivatedEnvelope.Success);
            Assert.NotNull(deactivatedEnvelope.Data);
            Assert.Equal(TenantStatus.Inactive, deactivatedEnvelope.Data.Status);
        }
        else
        {
            Assert.Equal(HttpStatusCode.NotFound, getAfterDelete.StatusCode);
        }
    }
}
