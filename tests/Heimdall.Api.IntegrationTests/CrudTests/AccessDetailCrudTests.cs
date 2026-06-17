using System.Net;
using System.Net.Http.Json;
using Heimdall.Api.Models;
using Heimdall.Application.Tenants.Commands.CreateTenant;
using Heimdall.Domain.Enums;

namespace Heimdall.Api.IntegrationTests.CrudTests;

/// <summary>
/// Full CRUD lifecycle integration tests for UserAccessDetail, GroupAccessDetail,
/// and FunctionalAreaAccessRequirement entities.
/// create → read → update → deactivate → verify state
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class AccessDetailCrudTests : IntegrationTestBase
{
    public AccessDetailCrudTests(HeimdallWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task UserAccessDetail_FullCrudLifecycle()
    {
        var (tenantId, appId) = await CreateTestTenantAndAppAsync();
        var userId = await CreateUserAsync(tenantId);

        // Create
        var createPayload = new
        {
            UserProfileId = userId,
            AccessDetailType = "Region",
            AccessDetailCode = "REGION_US",
            AccessDetailValue = "US-East",
            Description = "User access for US East region",
            ValidFrom = DateTime.UtcNow.AddDays(-1),
            ValidTo = DateTime.UtcNow.AddDays(365)
        };

        var createResponse = await Client.PostAsJsonAsync(
            $"/api/tenants/{tenantId}/applications/{appId}/access-details/users", createPayload);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var createEnvelope = await createResponse.Content.ReadFromJsonAsync<ApiEnvelope<AccessDetailResponse>>();
        Assert.NotNull(createEnvelope);
        Assert.True(createEnvelope.Success);
        Assert.NotNull(createEnvelope.Data);
        var adId = createEnvelope.Data.Id;
        Assert.NotEqual(Guid.Empty, adId);

        // Read
        var getResponse = await Client.GetAsync(
            $"/api/tenants/{tenantId}/applications/{appId}/access-details/users/{userId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var getEnvelope = await getResponse.Content.ReadFromJsonAsync<ApiEnvelope<List<UserAccessDetailDto>>>();
        Assert.NotNull(getEnvelope);
        Assert.True(getEnvelope.Success);
        Assert.NotNull(getEnvelope.Data);
        Assert.Contains(getEnvelope.Data, d => d.AccessDetailType == "Region" && d.AccessDetailCode == "REGION_US");

        // Update
        var updatePayload = new
        {
            AccessDetailValue = "US-West",
            Description = "Updated to US West",
            ValidFrom = DateTime.UtcNow,
            ValidTo = DateTime.UtcNow.AddDays(180)
        };

        var updateResponse = await Client.PutAsJsonAsync(
            $"/api/tenants/{tenantId}/applications/{appId}/access-details/users/{adId}", updatePayload);
        Assert.Equal(HttpStatusCode.NoContent, updateResponse.StatusCode);

        // Read after update
        var getAfterUpdate = await Client.GetAsync(
            $"/api/tenants/{tenantId}/applications/{appId}/access-details/users/{userId}");
        Assert.Equal(HttpStatusCode.OK, getAfterUpdate.StatusCode);

        var updatedEnvelope = await getAfterUpdate.Content.ReadFromJsonAsync<ApiEnvelope<List<UserAccessDetailDto>>>();
        Assert.NotNull(updatedEnvelope);
        Assert.NotNull(updatedEnvelope.Data);
        var updatedDetail = updatedEnvelope.Data.FirstOrDefault(d => d.AccessDetailCode == "REGION_US");
        Assert.NotNull(updatedDetail);
        Assert.Equal("US-West", updatedDetail.AccessDetailValue);

        // Deactivate
        var deleteResponse = await Client.DeleteAsync(
            $"/api/tenants/{tenantId}/applications/{appId}/access-details/users/{adId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task GroupAccessDetail_FullCrudLifecycle()
    {
        var (tenantId, appId) = await CreateTestTenantAndAppAsync();
        var groupId = await CreateGroupAsync(tenantId, appId);

        // Create
        var createPayload = new
        {
            GroupId = groupId,
            AccessDetailType = "Department",
            AccessDetailCode = "DEPT_ENG",
            AccessDetailValue = "Engineering",
            Description = "Engineering department access",
            ValidFrom = (DateTime?)null,
            ValidTo = (DateTime?)null
        };

        var createResponse = await Client.PostAsJsonAsync(
            $"/api/tenants/{tenantId}/applications/{appId}/access-details/groups", createPayload);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var createEnvelope = await createResponse.Content.ReadFromJsonAsync<ApiEnvelope<AccessDetailResponse>>();
        Assert.NotNull(createEnvelope);
        Assert.True(createEnvelope.Success);
        Assert.NotNull(createEnvelope.Data);
        var gadId = createEnvelope.Data.Id;
        Assert.NotEqual(Guid.Empty, gadId);

        // Read
        var getResponse = await Client.GetAsync(
            $"/api/tenants/{tenantId}/applications/{appId}/access-details/groups/{groupId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var getEnvelope = await getResponse.Content.ReadFromJsonAsync<ApiEnvelope<List<GroupAccessDetailDto>>>();
        Assert.NotNull(getEnvelope);
        Assert.True(getEnvelope.Success);
        Assert.NotNull(getEnvelope.Data);
        Assert.Contains(getEnvelope.Data, d => d.AccessDetailType == "Department" && d.AccessDetailCode == "DEPT_ENG");

        // Update
        var updatePayload = new
        {
            AccessDetailValue = "Engineering & QA",
            Description = "Updated department",
            ValidFrom = DateTime.UtcNow,
            ValidTo = DateTime.UtcNow.AddDays(90)
        };

        var updateResponse = await Client.PutAsJsonAsync(
            $"/api/tenants/{tenantId}/applications/{appId}/access-details/groups/{gadId}", updatePayload);
        Assert.Equal(HttpStatusCode.NoContent, updateResponse.StatusCode);

        // Read after update
        var getAfterUpdate = await Client.GetAsync(
            $"/api/tenants/{tenantId}/applications/{appId}/access-details/groups/{groupId}");
        Assert.Equal(HttpStatusCode.OK, getAfterUpdate.StatusCode);

        var updatedEnvelope = await getAfterUpdate.Content.ReadFromJsonAsync<ApiEnvelope<List<GroupAccessDetailDto>>>();
        Assert.NotNull(updatedEnvelope);
        Assert.NotNull(updatedEnvelope.Data);
        var updatedDetail = updatedEnvelope.Data.FirstOrDefault(d => d.AccessDetailCode == "DEPT_ENG");
        Assert.NotNull(updatedDetail);
        Assert.Equal("Engineering & QA", updatedDetail.AccessDetailValue);

        // Deactivate
        var deleteResponse = await Client.DeleteAsync(
            $"/api/tenants/{tenantId}/applications/{appId}/access-details/groups/{gadId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task FunctionalAreaAccessRequirement_FullCrudLifecycle()
    {
        var (tenantId, appId) = await CreateTestTenantAndAppAsync();
        var faId = await CreateFunctionalAreaAsync(tenantId, appId);

        // Create
        var createPayload = new
        {
            AccessDetailType = "Region",
            IsRequired = true,
            Description = "Region is required for this functional area"
        };

        var createResponse = await Client.PostAsJsonAsync(
            $"/api/tenants/{tenantId}/applications/{appId}/access-details/functional-areas/{faId}/requirements", createPayload);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var createEnvelope = await createResponse.Content.ReadFromJsonAsync<ApiEnvelope<FaRequirementResponse>>();
        Assert.NotNull(createEnvelope);
        Assert.True(createEnvelope.Success);
        Assert.NotNull(createEnvelope.Data);
        var reqId = createEnvelope.Data.Id;
        Assert.NotEqual(Guid.Empty, reqId);

        // Read
        var getResponse = await Client.GetAsync(
            $"/api/tenants/{tenantId}/applications/{appId}/access-details/functional-areas/{faId}/requirements");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var getEnvelope = await getResponse.Content.ReadFromJsonAsync<ApiEnvelope<List<FaRequirementDto>>>();
        Assert.NotNull(getEnvelope);
        Assert.True(getEnvelope.Success);
        Assert.NotNull(getEnvelope.Data);
        Assert.Contains(getEnvelope.Data, r => r.AccessDetailType == "Region" && r.IsRequired);

        // Update
        var updatePayload = new
        {
            IsRequired = false,
            Description = "Region is now optional"
        };

        var updateResponse = await Client.PutAsJsonAsync(
            $"/api/tenants/{tenantId}/applications/{appId}/access-details/functional-areas/requirements/{reqId}", updatePayload);
        Assert.Equal(HttpStatusCode.NoContent, updateResponse.StatusCode);

        // Read after update
        var getAfterUpdate = await Client.GetAsync(
            $"/api/tenants/{tenantId}/applications/{appId}/access-details/functional-areas/{faId}/requirements");
        Assert.Equal(HttpStatusCode.OK, getAfterUpdate.StatusCode);

        var updatedEnvelope = await getAfterUpdate.Content.ReadFromJsonAsync<ApiEnvelope<List<FaRequirementDto>>>();
        Assert.NotNull(updatedEnvelope);
        Assert.NotNull(updatedEnvelope.Data);
        var updated = updatedEnvelope.Data.FirstOrDefault(r => r.AccessDetailType == "Region");
        Assert.NotNull(updated);
        Assert.False(updated.IsRequired);

        // Deactivate
        var deleteResponse = await Client.DeleteAsync(
            $"/api/tenants/{tenantId}/applications/{appId}/access-details/functional-areas/requirements/{reqId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }

    private async Task<(Guid TenantId, Guid AppId)> CreateTestTenantAndAppAsync()
    {
        var slug = "ad-test-" + Guid.NewGuid().ToString("N")[..8];
        var tenantPayload = new CreateTenantCommand("AD Test Tenant", slug, PrimaryIdentityMode.EntraExternalId);
        var tenantResponse = await Client.PostAsJsonAsync("/api/tenants", tenantPayload);
        tenantResponse.EnsureSuccessStatusCode();
        var tenantEnvelope = await tenantResponse.Content.ReadFromJsonAsync<ApiEnvelope<CreateTenantResult>>();
        var tenantId = tenantEnvelope!.Data!.TenantId;

        var appPayload = new { Name = "AD Test App", ClientIdentifier = "ad-client-" + Guid.NewGuid().ToString("N")[..8] };
        var appResponse = await Client.PostAsJsonAsync($"/api/tenants/{tenantId}/applications", appPayload);
        appResponse.EnsureSuccessStatusCode();
        var appEnvelope = await appResponse.Content.ReadFromJsonAsync<ApiEnvelope<AppResponse>>();
        return (tenantId, appEnvelope!.Data!.ApplicationId);
    }

    private async Task<Guid> CreateUserAsync(Guid tenantId)
    {
        var payload = new
        {
            ExternalSubjectId = "ext-ad-" + Guid.NewGuid().ToString("N")[..8],
            IdentityProvider = "EntraExternalId",
            Email = $"ad-user-{Guid.NewGuid():N}@test.com",
            DisplayName = "AD Test User"
        };
        var response = await Client.PostAsJsonAsync($"/api/tenants/{tenantId}/users", payload);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<SyncUserResponse>>();
        return envelope!.Data!.UserProfileId;
    }

    private async Task<Guid> CreateGroupAsync(Guid tenantId, Guid appId)
    {
        var payload = new { Name = "AD Test Group " + Guid.NewGuid().ToString("N")[..6] };
        var response = await Client.PostAsJsonAsync(
            $"/api/tenants/{tenantId}/applications/{appId}/groups", payload);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<IdResponse>>();
        return envelope!.Data!.Id;
    }

    private async Task<Guid> CreateFunctionalAreaAsync(Guid tenantId, Guid appId)
    {
        var code = "FA_AD_" + Guid.NewGuid().ToString("N")[..6].ToUpper();
        var payload = new { FunctionalAreaCode = code, Name = "AD Test FA" };
        var response = await Client.PostAsJsonAsync(
            $"/api/tenants/{tenantId}/applications/{appId}/functional-areas", payload);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<IdResponse>>();
        return envelope!.Data!.Id;
    }

    private sealed record AccessDetailResponse(Guid Id);
    private sealed record UserAccessDetailDto(string AccessDetailType, string AccessDetailCode, string AccessDetailValue);
    private sealed record GroupAccessDetailDto(string AccessDetailType, string AccessDetailCode, string AccessDetailValue);
    private sealed record FaRequirementResponse(Guid Id);
    private sealed record FaRequirementDto(string AccessDetailType, bool IsRequired, string? Description);
    private sealed record AppResponse(Guid ApplicationId);
    private sealed record IdResponse(Guid Id);
    private sealed record SyncUserResponse(Guid UserProfileId, bool IsNewUser);
}
