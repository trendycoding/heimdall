using System.Net;
using System.Net.Http.Json;
using Heimdall.Api.Models;
using Heimdall.Application.Tenants.Commands.CreateTenant;
using Heimdall.Domain.Enums;

namespace Heimdall.Api.IntegrationTests.CrudTests;

/// <summary>
/// Full CRUD lifecycle integration tests for PermissionTemplate entity.
/// create → read → update → deactivate → verify state
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class PermissionTemplateCrudTests : IntegrationTestBase
{
    public PermissionTemplateCrudTests(HeimdallWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task PermissionTemplate_FullCrudLifecycle()
    {
        var (tenantId, appId) = await CreateTestTenantAndAppAsync();

        // Create
        var templateCode = "TMPL_" + Guid.NewGuid().ToString("N")[..8].ToUpper();
        var createPayload = new
        {
            TemplateCode = templateCode,
            Name = "Test Permission Template",
            Description = "A test permission template"
        };

        var createResponse = await Client.PostAsJsonAsync(
            $"/api/tenants/{tenantId}/applications/{appId}/permission-templates", createPayload);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var createEnvelope = await createResponse.Content.ReadFromJsonAsync<ApiEnvelope<TemplateResponse>>();
        Assert.NotNull(createEnvelope);
        Assert.True(createEnvelope.Success);
        Assert.NotNull(createEnvelope.Data);
        var templateId = createEnvelope.Data.PermissionTemplateId;
        Assert.NotEqual(Guid.Empty, templateId);

        // Read
        var getResponse = await Client.GetAsync(
            $"/api/tenants/{tenantId}/applications/{appId}/permission-templates/{templateId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var getEnvelope = await getResponse.Content.ReadFromJsonAsync<ApiEnvelope<TemplateDetailResponse>>();
        Assert.NotNull(getEnvelope);
        Assert.True(getEnvelope.Success);
        Assert.NotNull(getEnvelope.Data);
        Assert.Equal(templateId, getEnvelope.Data.PermissionTemplateId);
        Assert.Equal(templateCode, getEnvelope.Data.TemplateCode);
        Assert.Equal(createPayload.Name, getEnvelope.Data.Name);
        Assert.True(getEnvelope.Data.IsActive);

        // Update
        var updatePayload = new
        {
            Name = "Updated Template Name",
            Description = "Updated template description",
            IsActive = true
        };

        var updateResponse = await Client.PutAsJsonAsync(
            $"/api/tenants/{tenantId}/applications/{appId}/permission-templates/{templateId}", updatePayload);
        Assert.Equal(HttpStatusCode.NoContent, updateResponse.StatusCode);

        // Read after update
        var getAfterUpdate = await Client.GetAsync(
            $"/api/tenants/{tenantId}/applications/{appId}/permission-templates/{templateId}");
        Assert.Equal(HttpStatusCode.OK, getAfterUpdate.StatusCode);

        var updatedEnvelope = await getAfterUpdate.Content.ReadFromJsonAsync<ApiEnvelope<TemplateDetailResponse>>();
        Assert.NotNull(updatedEnvelope);
        Assert.True(updatedEnvelope.Success);
        Assert.NotNull(updatedEnvelope.Data);
        Assert.Equal("Updated Template Name", updatedEnvelope.Data.Name);
        Assert.Equal("Updated template description", updatedEnvelope.Data.Description);

        // Add permission entry to template
        var permId = await CreatePermissionAsync(tenantId, appId);
        var addPermPayload = new
        {
            PermissionId = permId,
            Effect = Effect.Allow,
            ValidFromOffsetDays = (int?)null,
            ValidToOffsetDays = 30
        };

        var addPermResponse = await Client.PostAsJsonAsync(
            $"/api/tenants/{tenantId}/applications/{appId}/permission-templates/{templateId}/permissions", addPermPayload);
        Assert.Equal(HttpStatusCode.Created, addPermResponse.StatusCode);

        // Add group entry to template
        var groupId = await CreateGroupAsync(tenantId, appId);
        var addGroupPayload = new { GroupId = groupId };

        var addGroupResponse = await Client.PostAsJsonAsync(
            $"/api/tenants/{tenantId}/applications/{appId}/permission-templates/{templateId}/groups", addGroupPayload);
        Assert.Equal(HttpStatusCode.Created, addGroupResponse.StatusCode);

        // Add access detail entry to template
        var addAdPayload = new
        {
            AccessDetailType = "Region",
            AccessDetailCode = "REGION_ALL",
            AccessDetailValue = "All Regions",
            Description = "Full region access",
            ValidFromOffsetDays = (int?)0,
            ValidToOffsetDays = 365
        };

        var addAdResponse = await Client.PostAsJsonAsync(
            $"/api/tenants/{tenantId}/applications/{appId}/permission-templates/{templateId}/access-details", addAdPayload);
        Assert.Equal(HttpStatusCode.Created, addAdResponse.StatusCode);

        // Verify template has entries by reading it again
        var getWithEntries = await Client.GetAsync(
            $"/api/tenants/{tenantId}/applications/{appId}/permission-templates/{templateId}");
        Assert.Equal(HttpStatusCode.OK, getWithEntries.StatusCode);

        // Deactivate
        var deleteResponse = await Client.DeleteAsync(
            $"/api/tenants/{tenantId}/applications/{appId}/permission-templates/{templateId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // Verify deactivated
        var getAfterDelete = await Client.GetAsync(
            $"/api/tenants/{tenantId}/applications/{appId}/permission-templates/{templateId}");
        if (getAfterDelete.StatusCode == HttpStatusCode.OK)
        {
            var deactivatedEnvelope = await getAfterDelete.Content.ReadFromJsonAsync<ApiEnvelope<TemplateDetailResponse>>();
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
        var slug = "tmpl-test-" + Guid.NewGuid().ToString("N")[..8];
        var tenantPayload = new CreateTenantCommand("Template Test Tenant", slug, PrimaryIdentityMode.EntraExternalId);
        var tenantResponse = await Client.PostAsJsonAsync("/api/tenants", tenantPayload);
        tenantResponse.EnsureSuccessStatusCode();
        var tenantEnvelope = await tenantResponse.Content.ReadFromJsonAsync<ApiEnvelope<CreateTenantResult>>();
        var tenantId = tenantEnvelope!.Data!.TenantId;

        var appPayload = new { Name = "Template Test App", ClientIdentifier = "tmpl-client-" + Guid.NewGuid().ToString("N")[..8] };
        var appResponse = await Client.PostAsJsonAsync($"/api/tenants/{tenantId}/applications", appPayload);
        appResponse.EnsureSuccessStatusCode();
        var appEnvelope = await appResponse.Content.ReadFromJsonAsync<ApiEnvelope<AppResponse>>();
        return (tenantId, appEnvelope!.Data!.ApplicationId);
    }

    private async Task<Guid> CreatePermissionAsync(Guid tenantId, Guid appId)
    {
        var faCode = "FA_TMPL_" + Guid.NewGuid().ToString("N")[..4].ToUpper();
        var faPayload = new { FunctionalAreaCode = faCode, Name = "Template FA" };
        var faResponse = await Client.PostAsJsonAsync(
            $"/api/tenants/{tenantId}/applications/{appId}/functional-areas", faPayload);
        faResponse.EnsureSuccessStatusCode();
        var faEnvelope = await faResponse.Content.ReadFromJsonAsync<ApiEnvelope<IdResponse>>();
        var faId = faEnvelope!.Data!.Id;

        var ptCode = "PT_TMPL_" + Guid.NewGuid().ToString("N")[..4].ToUpper();
        var ptPayload = new { Code = ptCode, Name = "Template PT" };
        var ptResponse = await Client.PostAsJsonAsync(
            $"/api/tenants/{tenantId}/applications/{appId}/permission-types", ptPayload);
        ptResponse.EnsureSuccessStatusCode();
        var ptEnvelope = await ptResponse.Content.ReadFromJsonAsync<ApiEnvelope<IdResponse>>();
        var ptId = ptEnvelope!.Data!.Id;

        var permCode = "PERM_TMPL_" + Guid.NewGuid().ToString("N")[..4].ToUpper();
        var permPayload = new { FunctionalAreaId = faId, PermissionTypeId = ptId, PermissionCode = permCode, Name = "Template Permission" };
        var permResponse = await Client.PostAsJsonAsync(
            $"/api/tenants/{tenantId}/applications/{appId}/permissions", permPayload);
        permResponse.EnsureSuccessStatusCode();
        var permEnvelope = await permResponse.Content.ReadFromJsonAsync<ApiEnvelope<IdResponse>>();
        return permEnvelope!.Data!.Id;
    }

    private async Task<Guid> CreateGroupAsync(Guid tenantId, Guid appId)
    {
        var payload = new { Name = "Template Test Group " + Guid.NewGuid().ToString("N")[..6] };
        var response = await Client.PostAsJsonAsync(
            $"/api/tenants/{tenantId}/applications/{appId}/groups", payload);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<IdResponse>>();
        return envelope!.Data!.Id;
    }

    private sealed record TemplateResponse(Guid PermissionTemplateId);
    private sealed record TemplateDetailResponse(Guid PermissionTemplateId, string TemplateCode, string Name, string? Description, bool IsActive);
    private sealed record AppResponse(Guid ApplicationId);
    private sealed record IdResponse(Guid Id);
}
