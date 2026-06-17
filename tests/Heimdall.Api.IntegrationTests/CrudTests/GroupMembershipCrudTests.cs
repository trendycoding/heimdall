using System.Net;
using System.Net.Http.Json;
using Heimdall.Api.Models;
using Heimdall.Application.Tenants.Commands.CreateTenant;
using Heimdall.Domain.Enums;

namespace Heimdall.Api.IntegrationTests.CrudTests;

/// <summary>
/// Full CRUD lifecycle integration tests for GroupMembership entity.
/// create → read → delete → verify state
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class GroupMembershipCrudTests : IntegrationTestBase
{
    public GroupMembershipCrudTests(HeimdallWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task GroupMembership_FullCrudLifecycle()
    {
        var (tenantId, appId) = await CreateTestTenantAndAppAsync();
        var groupId = await CreateGroupAsync(tenantId, appId);
        var userId = await CreateUserAsync(tenantId);

        // Create membership
        var addMemberPayload = new { UserProfileId = userId };
        var createResponse = await Client.PostAsJsonAsync(
            $"/api/tenants/{tenantId}/applications/{appId}/groups/{groupId}/members", addMemberPayload);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var createEnvelope = await createResponse.Content.ReadFromJsonAsync<ApiEnvelope<MembershipResponse>>();
        Assert.NotNull(createEnvelope);
        Assert.True(createEnvelope.Success);
        Assert.NotNull(createEnvelope.Data);
        var membershipId = createEnvelope.Data.Id;
        Assert.NotEqual(Guid.Empty, membershipId);

        // Read members
        var getResponse = await Client.GetAsync(
            $"/api/tenants/{tenantId}/applications/{appId}/groups/{groupId}/members");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var getEnvelope = await getResponse.Content.ReadFromJsonAsync<ApiEnvelope<List<MemberDto>>>();
        Assert.NotNull(getEnvelope);
        Assert.True(getEnvelope.Success);
        Assert.NotNull(getEnvelope.Data);
        Assert.Contains(getEnvelope.Data, m => m.UserProfileId == userId);

        // Delete membership
        var deleteResponse = await Client.DeleteAsync(
            $"/api/tenants/{tenantId}/applications/{appId}/groups/{groupId}/members/{membershipId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // Verify removed
        var getAfterDelete = await Client.GetAsync(
            $"/api/tenants/{tenantId}/applications/{appId}/groups/{groupId}/members");
        Assert.Equal(HttpStatusCode.OK, getAfterDelete.StatusCode);

        var afterDeleteEnvelope = await getAfterDelete.Content.ReadFromJsonAsync<ApiEnvelope<List<MemberDto>>>();
        Assert.NotNull(afterDeleteEnvelope);
        Assert.True(afterDeleteEnvelope.Success);
        Assert.NotNull(afterDeleteEnvelope.Data);
        Assert.DoesNotContain(afterDeleteEnvelope.Data, m => m.UserProfileId == userId);
    }

    private async Task<(Guid TenantId, Guid AppId)> CreateTestTenantAndAppAsync()
    {
        var slug = "gm-test-" + Guid.NewGuid().ToString("N")[..8];
        var tenantPayload = new CreateTenantCommand("GM Test Tenant", slug, PrimaryIdentityMode.EntraExternalId);
        var tenantResponse = await Client.PostAsJsonAsync("/api/tenants", tenantPayload);
        tenantResponse.EnsureSuccessStatusCode();
        var tenantEnvelope = await tenantResponse.Content.ReadFromJsonAsync<ApiEnvelope<CreateTenantResult>>();
        var tenantId = tenantEnvelope!.Data!.TenantId;

        var appPayload = new { Name = "GM Test App", ClientIdentifier = "gm-client-" + Guid.NewGuid().ToString("N")[..8] };
        var appResponse = await Client.PostAsJsonAsync($"/api/tenants/{tenantId}/applications", appPayload);
        appResponse.EnsureSuccessStatusCode();
        var appEnvelope = await appResponse.Content.ReadFromJsonAsync<ApiEnvelope<AppResponse>>();
        return (tenantId, appEnvelope!.Data!.ApplicationId);
    }

    private async Task<Guid> CreateGroupAsync(Guid tenantId, Guid appId)
    {
        var payload = new { Name = "Membership Test Group " + Guid.NewGuid().ToString("N")[..6] };
        var response = await Client.PostAsJsonAsync(
            $"/api/tenants/{tenantId}/applications/{appId}/groups", payload);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<IdResponse>>();
        return envelope!.Data!.Id;
    }

    private async Task<Guid> CreateUserAsync(Guid tenantId)
    {
        var payload = new
        {
            ExternalSubjectId = "ext-member-" + Guid.NewGuid().ToString("N")[..8],
            IdentityProvider = "EntraExternalId",
            Email = $"member-{Guid.NewGuid():N}@test.com",
            DisplayName = "Test Member"
        };
        var response = await Client.PostAsJsonAsync($"/api/tenants/{tenantId}/users", payload);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<SyncUserResponse>>();
        return envelope!.Data!.UserProfileId;
    }

    private sealed record MembershipResponse(Guid Id);
    private sealed record MemberDto(Guid UserProfileId, string? DisplayName);
    private sealed record AppResponse(Guid ApplicationId);
    private sealed record IdResponse(Guid Id);
    private sealed record SyncUserResponse(Guid UserProfileId, bool IsNewUser);
}
