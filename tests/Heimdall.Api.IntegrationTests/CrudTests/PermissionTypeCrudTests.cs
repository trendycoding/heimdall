using System.Net;
using System.Net.Http.Json;
using Heimdall.Api.Models;
using Heimdall.Application.Tenants.Commands.CreateTenant;
using Heimdall.Domain.Enums;

namespace Heimdall.Api.IntegrationTests.CrudTests;

/// <summary>
/// Full CRUD lifecycle integration tests for PermissionType entity.
/// create → read → update → deactivate → verify state
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class PermissionTypeCrudTests : IntegrationTestBase
{
    public PermissionTypeCrudTests(HeimdallWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task PermissionType_FullCrudLifecycle()
    {
        var (tenantId, appId) = await CreateTestTenantAndAppAsync();

        // Create
        var code = "PT_TEST_" + Guid.NewGuid().ToString("N")[..6].ToUpper();
        var createPayload = new
        {
            Code = code,
            Name = "Test Permission Type",
            Description = "A test permission type",
            IsSystemReserved = false
        };

        var createResponse = await Client.PostAsJsonAsync(
            $"/api/tenants/{tenantId}/applications/{appId}/permission-types", createPayload);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var createEnvelope = await createResponse.Content.ReadFromJsonAsync<ApiEnvelope<PermissionTypeResponse>>();
        Assert.NotNull(createEnvelope);
        Assert.True(createEnvelope.Success);
        Assert.NotNull(createEnvelope.Data);
        var ptId = createEnvelope.Data.Id;
        Assert.NotEqual(Guid.Empty, ptId);

        // Read
        var getResponse = await Client.GetAsync(
            $"/api/tenants/{tenantId}/applications/{appId}/permission-types/{ptId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var getEnvelope = await getResponse.Content.ReadFromJsonAsync<ApiEnvelope<PermissionTypeResponse>>();
        Assert.NotNull(getEnvelope);
        Assert.True(getEnvelope.Success);
        Assert.NotNull(getEnvelope.Data);
        Assert.Equal(ptId, getEnvelope.Data.Id);
        Assert.Equal(code, getEnvelope.Data.Code);
        Assert.Equal(createPayload.Name, getEnvelope.Data.Name);
        Assert.True(getEnvelope.Data.IsActive);

        // Update
        var newCode = "PT_UPDATED_" + Guid.NewGuid().ToString("N")[..4].ToUpper();
        var updatePayload = new
        {
            Code = newCode,
            Name = "Updated Permission Type",
            Description = "Updated description"
        };

        var updateResponse = await Client.PutAsJsonAsync(
            $"/api/tenants/{tenantId}/applications/{appId}/permission-types/{ptId}", updatePayload);
        Assert.Equal(HttpStatusCode.NoContent, updateResponse.StatusCode);

        // Read after update
        var getAfterUpdate = await Client.GetAsync(
            $"/api/tenants/{tenantId}/applications/{appId}/permission-types/{ptId}");
        Assert.Equal(HttpStatusCode.OK, getAfterUpdate.StatusCode);

        var updatedEnvelope = await getAfterUpdate.Content.ReadFromJsonAsync<ApiEnvelope<PermissionTypeResponse>>();
        Assert.NotNull(updatedEnvelope);
        Assert.True(updatedEnvelope.Success);
        Assert.NotNull(updatedEnvelope.Data);
        Assert.Equal("Updated Permission Type", updatedEnvelope.Data.Name);

        // Deactivate
        var deleteResponse = await Client.DeleteAsync(
            $"/api/tenants/{tenantId}/applications/{appId}/permission-types/{ptId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // Verify deactivated
        var getAfterDelete = await Client.GetAsync(
            $"/api/tenants/{tenantId}/applications/{appId}/permission-types/{ptId}");
        if (getAfterDelete.StatusCode == HttpStatusCode.OK)
        {
            var deactivatedEnvelope = await getAfterDelete.Content.ReadFromJsonAsync<ApiEnvelope<PermissionTypeResponse>>();
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
        var slug = "pt-test-" + Guid.NewGuid().ToString("N")[..8];
        var tenantPayload = new CreateTenantCommand("PT Test Tenant", slug, PrimaryIdentityMode.EntraExternalId);
        var tenantResponse = await Client.PostAsJsonAsync("/api/tenants", tenantPayload);
        tenantResponse.EnsureSuccessStatusCode();
        var tenantEnvelope = await tenantResponse.Content.ReadFromJsonAsync<ApiEnvelope<CreateTenantResult>>();
        var tenantId = tenantEnvelope!.Data!.TenantId;

        var appPayload = new { Name = "PT Test App", ClientIdentifier = "pt-client-" + Guid.NewGuid().ToString("N")[..8] };
        var appResponse = await Client.PostAsJsonAsync($"/api/tenants/{tenantId}/applications", appPayload);
        appResponse.EnsureSuccessStatusCode();
        var appEnvelope = await appResponse.Content.ReadFromJsonAsync<ApiEnvelope<AppResponse>>();
        return (tenantId, appEnvelope!.Data!.ApplicationId);
    }

    private sealed record PermissionTypeResponse(Guid Id, string Code, string Name, string? Description, bool IsActive);
    private sealed record AppResponse(Guid ApplicationId);
}
