using System.Net;
using System.Net.Http.Json;
using Heimdall.Api.Models;
using Heimdall.Application.Tenants.Commands.CreateTenant;
using Heimdall.Domain.Enums;

namespace Heimdall.Api.IntegrationTests.CrudTests;

/// <summary>
/// Full CRUD lifecycle integration tests for PermissionAssignment entities (User + Group).
/// create → read → delete → verify state
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class PermissionAssignmentCrudTests : IntegrationTestBase
{
    public PermissionAssignmentCrudTests(HeimdallWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task UserPermissionAssignment_FullCrudLifecycle()
    {
        var (tenantId, appId) = await CreateTestTenantAndAppAsync();
        var userId = await CreateUserAsync(tenantId);
        var permId = await CreatePermissionAsync(tenantId, appId);

        // Create user permission assignment
        var createPayload = new
        {
            UserProfileId = userId,
            PermissionId = permId,
            Effect = Effect.Allow,
            ValidFrom = DateTime.UtcNow.AddDays(-1),
            ValidTo = DateTime.UtcNow.AddDays(30)
        };

        var createResponse = await Client.PostAsJsonAsync(
            $"/api/tenants/{tenantId}/applications/{appId}/permission-assignments/users", createPayload);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var createEnvelope = await createResponse.Content.ReadFromJsonAsync<ApiEnvelope<AssignmentResponse>>();
        Assert.NotNull(createEnvelope);
        Assert.True(createEnvelope.Success);
        Assert.NotNull(createEnvelope.Data);
        var assignmentId = createEnvelope.Data.Id;
        Assert.NotEqual(Guid.Empty, assignmentId);

        // Read user assignments
        var getResponse = await Client.GetAsync(
            $"/api/tenants/{tenantId}/applications/{appId}/permission-assignments/users/{userId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var getEnvelope = await getResponse.Content.ReadFromJsonAsync<ApiEnvelope<List<UserAssignmentDto>>>();
        Assert.NotNull(getEnvelope);
        Assert.True(getEnvelope.Success);
        Assert.NotNull(getEnvelope.Data);
        Assert.Contains(getEnvelope.Data, a => a.PermissionId == permId);

        // Delete assignment
        var deleteResponse = await Client.DeleteAsync(
            $"/api/tenants/{tenantId}/applications/{appId}/permission-assignments/users/{assignmentId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // Verify removed
        var getAfterDelete = await Client.GetAsync(
            $"/api/tenants/{tenantId}/applications/{appId}/permission-assignments/users/{userId}");
        Assert.Equal(HttpStatusCode.OK, getAfterDelete.StatusCode);

        var afterDeleteEnvelope = await getAfterDelete.Content.ReadFromJsonAsync<ApiEnvelope<List<UserAssignmentDto>>>();
        Assert.NotNull(afterDeleteEnvelope);
        Assert.True(afterDeleteEnvelope.Success);
        Assert.NotNull(afterDeleteEnvelope.Data);
        Assert.DoesNotContain(afterDeleteEnvelope.Data, a => a.Id == assignmentId);
    }

    [Fact]
    public async Task GroupPermissionAssignment_FullCrudLifecycle()
    {
        var (tenantId, appId) = await CreateTestTenantAndAppAsync();
        var groupId = await CreateGroupAsync(tenantId, appId);
        var permId = await CreatePermissionAsync(tenantId, appId);

        // Create group permission assignment
        var createPayload = new
        {
            GroupId = groupId,
            PermissionId = permId,
            Effect = Effect.Deny,
            ValidFrom = (DateTime?)null,
            ValidTo = (DateTime?)null
        };

        var createResponse = await Client.PostAsJsonAsync(
            $"/api/tenants/{tenantId}/applications/{appId}/permission-assignments/groups", createPayload);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var createEnvelope = await createResponse.Content.ReadFromJsonAsync<ApiEnvelope<AssignmentResponse>>();
        Assert.NotNull(createEnvelope);
        Assert.True(createEnvelope.Success);
        Assert.NotNull(createEnvelope.Data);
        var assignmentId = createEnvelope.Data.Id;
        Assert.NotEqual(Guid.Empty, assignmentId);

        // Read group assignments
        var getResponse = await Client.GetAsync(
            $"/api/tenants/{tenantId}/applications/{appId}/permission-assignments/groups/{groupId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var getEnvelope = await getResponse.Content.ReadFromJsonAsync<ApiEnvelope<List<GroupAssignmentDto>>>();
        Assert.NotNull(getEnvelope);
        Assert.True(getEnvelope.Success);
        Assert.NotNull(getEnvelope.Data);
        Assert.Contains(getEnvelope.Data, a => a.PermissionId == permId);

        // Delete assignment
        var deleteResponse = await Client.DeleteAsync(
            $"/api/tenants/{tenantId}/applications/{appId}/permission-assignments/groups/{assignmentId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // Verify removed
        var getAfterDelete = await Client.GetAsync(
            $"/api/tenants/{tenantId}/applications/{appId}/permission-assignments/groups/{groupId}");
        Assert.Equal(HttpStatusCode.OK, getAfterDelete.StatusCode);

        var afterDeleteEnvelope = await getAfterDelete.Content.ReadFromJsonAsync<ApiEnvelope<List<GroupAssignmentDto>>>();
        Assert.NotNull(afterDeleteEnvelope);
        Assert.True(afterDeleteEnvelope.Success);
        Assert.NotNull(afterDeleteEnvelope.Data);
        Assert.DoesNotContain(afterDeleteEnvelope.Data, a => a.Id == assignmentId);
    }

    private async Task<(Guid TenantId, Guid AppId)> CreateTestTenantAndAppAsync()
    {
        var slug = "pa-test-" + Guid.NewGuid().ToString("N")[..8];
        var tenantPayload = new CreateTenantCommand("PA Test Tenant", slug, PrimaryIdentityMode.EntraExternalId);
        var tenantResponse = await Client.PostAsJsonAsync("/api/tenants", tenantPayload);
        tenantResponse.EnsureSuccessStatusCode();
        var tenantEnvelope = await tenantResponse.Content.ReadFromJsonAsync<ApiEnvelope<CreateTenantResult>>();
        var tenantId = tenantEnvelope!.Data!.TenantId;

        var appPayload = new { Name = "PA Test App", ClientIdentifier = "pa-client-" + Guid.NewGuid().ToString("N")[..8] };
        var appResponse = await Client.PostAsJsonAsync($"/api/tenants/{tenantId}/applications", appPayload);
        appResponse.EnsureSuccessStatusCode();
        var appEnvelope = await appResponse.Content.ReadFromJsonAsync<ApiEnvelope<AppResponse>>();
        return (tenantId, appEnvelope!.Data!.ApplicationId);
    }

    private async Task<Guid> CreateUserAsync(Guid tenantId)
    {
        var payload = new
        {
            ExternalSubjectId = "ext-pa-" + Guid.NewGuid().ToString("N")[..8],
            IdentityProvider = "EntraExternalId",
            Email = $"pa-user-{Guid.NewGuid():N}@test.com",
            DisplayName = "PA Test User"
        };
        var response = await Client.PostAsJsonAsync($"/api/tenants/{tenantId}/users", payload);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<SyncUserResponse>>();
        return envelope!.Data!.UserProfileId;
    }

    private async Task<Guid> CreateGroupAsync(Guid tenantId, Guid appId)
    {
        var payload = new { Name = "PA Test Group " + Guid.NewGuid().ToString("N")[..6] };
        var response = await Client.PostAsJsonAsync(
            $"/api/tenants/{tenantId}/applications/{appId}/groups", payload);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<IdResponse>>();
        return envelope!.Data!.Id;
    }

    private async Task<Guid> CreatePermissionAsync(Guid tenantId, Guid appId)
    {
        var faCode = "FA_PA_" + Guid.NewGuid().ToString("N")[..4].ToUpper();
        var faPayload = new { FunctionalAreaCode = faCode, Name = "PA FA" };
        var faResponse = await Client.PostAsJsonAsync(
            $"/api/tenants/{tenantId}/applications/{appId}/functional-areas", faPayload);
        faResponse.EnsureSuccessStatusCode();
        var faEnvelope = await faResponse.Content.ReadFromJsonAsync<ApiEnvelope<IdResponse>>();
        var faId = faEnvelope!.Data!.Id;

        var ptCode = "PT_PA_" + Guid.NewGuid().ToString("N")[..4].ToUpper();
        var ptPayload = new { Code = ptCode, Name = "PA PT" };
        var ptResponse = await Client.PostAsJsonAsync(
            $"/api/tenants/{tenantId}/applications/{appId}/permission-types", ptPayload);
        ptResponse.EnsureSuccessStatusCode();
        var ptEnvelope = await ptResponse.Content.ReadFromJsonAsync<ApiEnvelope<IdResponse>>();
        var ptId = ptEnvelope!.Data!.Id;

        var permCode = "PERM_PA_" + Guid.NewGuid().ToString("N")[..4].ToUpper();
        var permPayload = new { FunctionalAreaId = faId, PermissionTypeId = ptId, PermissionCode = permCode, Name = "PA Permission" };
        var permResponse = await Client.PostAsJsonAsync(
            $"/api/tenants/{tenantId}/applications/{appId}/permissions", permPayload);
        permResponse.EnsureSuccessStatusCode();
        var permEnvelope = await permResponse.Content.ReadFromJsonAsync<ApiEnvelope<IdResponse>>();
        return permEnvelope!.Data!.Id;
    }

    private sealed record AssignmentResponse(Guid Id);
    private sealed record UserAssignmentDto(Guid Id, Guid PermissionId, string Effect);
    private sealed record GroupAssignmentDto(Guid Id, Guid PermissionId, string Effect);
    private sealed record AppResponse(Guid ApplicationId);
    private sealed record IdResponse(Guid Id);
    private sealed record SyncUserResponse(Guid UserProfileId, bool IsNewUser);
}
