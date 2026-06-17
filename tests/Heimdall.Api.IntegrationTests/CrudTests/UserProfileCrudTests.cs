using System.Net;
using System.Net.Http.Json;
using Heimdall.Api.Models;
using Heimdall.Application.Tenants.Commands.CreateTenant;
using Heimdall.Domain.Enums;

namespace Heimdall.Api.IntegrationTests.CrudTests;

/// <summary>
/// Full CRUD lifecycle integration tests for UserProfile entity.
/// create (sync) → read → update → verify state
/// Note: Users are created via sync endpoint (POST), not a traditional create.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class UserProfileCrudTests : IntegrationTestBase
{
    public UserProfileCrudTests(HeimdallWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task UserProfile_FullCrudLifecycle()
    {
        var tenantId = await CreateTestTenantAsync();

        // Create (sync) user
        var createPayload = new
        {
            ExternalSubjectId = "ext-" + Guid.NewGuid().ToString("N")[..12],
            IdentityProvider = "EntraExternalId",
            Email = $"testuser-{Guid.NewGuid():N}@test.com",
            DisplayName = "Test User",
            Applications = (object?)null
        };

        var createResponse = await Client.PostAsJsonAsync($"/api/tenants/{tenantId}/users", createPayload);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var createEnvelope = await createResponse.Content.ReadFromJsonAsync<ApiEnvelope<SyncUserResponse>>();
        Assert.NotNull(createEnvelope);
        Assert.True(createEnvelope.Success);
        Assert.NotNull(createEnvelope.Data);
        var userId = createEnvelope.Data.UserProfileId;
        Assert.NotEqual(Guid.Empty, userId);
        Assert.True(createEnvelope.Data.IsNewUser);

        // Read
        var getResponse = await Client.GetAsync($"/api/tenants/{tenantId}/users/{userId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var getEnvelope = await getResponse.Content.ReadFromJsonAsync<ApiEnvelope<UserResponse>>();
        Assert.NotNull(getEnvelope);
        Assert.True(getEnvelope.Success);
        Assert.NotNull(getEnvelope.Data);
        Assert.Equal(userId, getEnvelope.Data.UserProfileId);
        Assert.Equal(createPayload.Email, getEnvelope.Data.Email);
        Assert.Equal(createPayload.DisplayName, getEnvelope.Data.DisplayName);

        // Update
        var updatePayload = new
        {
            Email = $"updated-{Guid.NewGuid():N}@test.com",
            DisplayName = "Updated User Name"
        };

        var updateResponse = await Client.PutAsJsonAsync($"/api/tenants/{tenantId}/users/{userId}", updatePayload);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        // Read after update
        var getAfterUpdate = await Client.GetAsync($"/api/tenants/{tenantId}/users/{userId}");
        Assert.Equal(HttpStatusCode.OK, getAfterUpdate.StatusCode);

        var updatedEnvelope = await getAfterUpdate.Content.ReadFromJsonAsync<ApiEnvelope<UserResponse>>();
        Assert.NotNull(updatedEnvelope);
        Assert.True(updatedEnvelope.Success);
        Assert.NotNull(updatedEnvelope.Data);
        Assert.Equal(updatePayload.Email, updatedEnvelope.Data.Email);
        Assert.Equal(updatePayload.DisplayName, updatedEnvelope.Data.DisplayName);

        // Sync again (upsert should return 200 not 201)
        var resyncPayload = new
        {
            ExternalSubjectId = createPayload.ExternalSubjectId,
            IdentityProvider = createPayload.IdentityProvider,
            Email = updatePayload.Email,
            DisplayName = "Resynced User",
            Applications = (object?)null
        };

        var resyncResponse = await Client.PostAsJsonAsync($"/api/tenants/{tenantId}/users", resyncPayload);
        Assert.Equal(HttpStatusCode.OK, resyncResponse.StatusCode);

        var resyncEnvelope = await resyncResponse.Content.ReadFromJsonAsync<ApiEnvelope<SyncUserResponse>>();
        Assert.NotNull(resyncEnvelope);
        Assert.True(resyncEnvelope.Success);
        Assert.NotNull(resyncEnvelope.Data);
        Assert.False(resyncEnvelope.Data.IsNewUser);
        Assert.Equal(userId, resyncEnvelope.Data.UserProfileId);
    }

    private async Task<Guid> CreateTestTenantAsync()
    {
        var slug = "user-test-" + Guid.NewGuid().ToString("N")[..8];
        var payload = new CreateTenantCommand("User Test Tenant", slug, PrimaryIdentityMode.EntraExternalId);
        var response = await Client.PostAsJsonAsync("/api/tenants", payload);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<CreateTenantResult>>();
        return envelope!.Data!.TenantId;
    }

    private sealed record SyncUserResponse(Guid UserProfileId, bool IsNewUser);
    private sealed record UserResponse(Guid UserProfileId, string Email, string DisplayName, string? Status);
}
