using System.Net;
using System.Net.Http.Json;
using Heimdall.Api.Models;
using Heimdall.Application.Tenants.Commands.CreateTenant;
using Heimdall.Domain.Enums;

namespace Heimdall.Api.IntegrationTests.CrudTests;

/// <summary>
/// Full CRUD lifecycle integration tests for Permission entity.
/// create → read → update → deactivate → verify state
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class PermissionCrudTests : IntegrationTestBase
{
    public PermissionCrudTests(HeimdallWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Permission_FullCrudLifecycle()
    {
        var (tenantId, appId) = await CreateTestTenantAndAppAsync();
        var faId = await CreateFunctionalAreaAsync(tenantId, appId);
        var ptId = await CreatePermissionTypeAsync(tenantId, appId);

        // Create
        var permCode = "PERM_TEST_" + Guid.NewGuid().ToString("N")[..6].ToUpper();
        var createPayload = new
        {
            FunctionalAreaId = faId,
            PermissionTypeId = ptId,
            PermissionCode = permCode,
            Name = "Test Permission",
            Description = "A test permission for integration testing"
        };

        var createResponse = await Client.PostAsJsonAsync(
            $"/api/tenants/{tenantId}/applications/{appId}/permissions", createPayload);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var createEnvelope = await createResponse.Content.ReadFromJsonAsync<ApiEnvelope<PermissionResponse>>();
        Assert.NotNull(createEnvelope);
        Assert.True(createEnvelope.Success);
        Assert.NotNull(createEnvelope.Data);
        var permId = createEnvelope.Data.Id;
        Assert.NotEqual(Guid.Empty, permId);

        // Read
        var getResponse = await Client.GetAsync(
            $"/api/tenants/{tenantId}/applications/{appId}/permissions/{permId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var getEnvelope = await getResponse.Content.ReadFromJsonAsync<ApiEnvelope<PermissionResponse>>();
        Assert.NotNull(getEnvelope);
        Assert.True(getEnvelope.Success);
        Assert.NotNull(getEnvelope.Data);
        Assert.Equal(permId, getEnvelope.Data.Id);
        Assert.Equal(permCode, getEnvelope.Data.PermissionCode);
        Assert.Equal(createPayload.Name, getEnvelope.Data.Name);

        // Update
        var newCode = "PERM_UPDATED_" + Guid.NewGuid().ToString("N")[..4].ToUpper();
        var updatePayload = new
        {
            PermissionCode = newCode,
            Name = "Updated Permission",
            Description = "Updated description"
        };

        var updateResponse = await Client.PutAsJsonAsync(
            $"/api/tenants/{tenantId}/applications/{appId}/permissions/{permId}", updatePayload);
        Assert.Equal(HttpStatusCode.NoContent, updateResponse.StatusCode);

        // Read after update
        var getAfterUpdate = await Client.GetAsync(
            $"/api/tenants/{tenantId}/applications/{appId}/permissions/{permId}");
        Assert.Equal(HttpStatusCode.OK, getAfterUpdate.StatusCode);

        var updatedEnvelope = await getAfterUpdate.Content.ReadFromJsonAsync<ApiEnvelope<PermissionResponse>>();
        Assert.NotNull(updatedEnvelope);
        Assert.True(updatedEnvelope.Success);
        Assert.NotNull(updatedEnvelope.Data);
        Assert.Equal("Updated Permission", updatedEnvelope.Data.Name);

        // Deactivate
        var deleteResponse = await Client.DeleteAsync(
            $"/api/tenants/{tenantId}/applications/{appId}/permissions/{permId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // Verify deactivated
        var getAfterDelete = await Client.GetAsync(
            $"/api/tenants/{tenantId}/applications/{appId}/permissions/{permId}");
        if (getAfterDelete.StatusCode == HttpStatusCode.OK)
        {
            var deactivatedEnvelope = await getAfterDelete.Content.ReadFromJsonAsync<ApiEnvelope<PermissionResponse>>();
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
        var slug = "perm-test-" + Guid.NewGuid().ToString("N")[..8];
        var tenantPayload = new CreateTenantCommand("Perm Test Tenant", slug, PrimaryIdentityMode.EntraExternalId);
        var tenantResponse = await Client.PostAsJsonAsync("/api/tenants", tenantPayload);
        tenantResponse.EnsureSuccessStatusCode();
        var tenantEnvelope = await tenantResponse.Content.ReadFromJsonAsync<ApiEnvelope<CreateTenantResult>>();
        var tenantId = tenantEnvelope!.Data!.TenantId;

        var appPayload = new { Name = "Perm Test App", ClientIdentifier = "perm-client-" + Guid.NewGuid().ToString("N")[..8] };
        var appResponse = await Client.PostAsJsonAsync($"/api/tenants/{tenantId}/applications", appPayload);
        appResponse.EnsureSuccessStatusCode();
        var appEnvelope = await appResponse.Content.ReadFromJsonAsync<ApiEnvelope<AppResponse>>();
        return (tenantId, appEnvelope!.Data!.ApplicationId);
    }

    private async Task<Guid> CreateFunctionalAreaAsync(Guid tenantId, Guid appId)
    {
        var code = "FA_" + Guid.NewGuid().ToString("N")[..6].ToUpper();
        var payload = new { FunctionalAreaCode = code, Name = "Test FA" };
        var response = await Client.PostAsJsonAsync(
            $"/api/tenants/{tenantId}/applications/{appId}/functional-areas", payload);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<IdResponse>>();
        return envelope!.Data!.Id;
    }

    private async Task<Guid> CreatePermissionTypeAsync(Guid tenantId, Guid appId)
    {
        var code = "PT_" + Guid.NewGuid().ToString("N")[..6].ToUpper();
        var payload = new { Code = code, Name = "Test PT" };
        var response = await Client.PostAsJsonAsync(
            $"/api/tenants/{tenantId}/applications/{appId}/permission-types", payload);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<IdResponse>>();
        return envelope!.Data!.Id;
    }

    private sealed record PermissionResponse(Guid Id, string PermissionCode, string Name, string? Description, bool IsActive);
    private sealed record AppResponse(Guid ApplicationId);
    private sealed record IdResponse(Guid Id);
}
