using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Heimdall.Api.Models;
using Heimdall.Domain.Entities;
using Heimdall.Domain.Enums;
using Heimdall.Domain.Models;
using Heimdall.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Heimdall.Api.IntegrationTests.AccessCheckTests;

/// <summary>
/// Integration tests for the AccessChecksController endpoints.
/// Tests cover:
/// - Single permission check by FunctionalAreaCode + PermissionTypeCode
/// - Single permission check by PermissionCode
/// - Single permission check by ExternalSubjectId + IdentityProvider
/// - Batch permission check (multiple permissions in single call)
/// - Effective permissions retrieval
///
/// **Validates: Requirements 27.7**
/// </summary>
public class AccessCheckEndpointTests : IntegrationTestBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // Shared test data identifiers
    private readonly Guid _tenantId;
    private readonly Guid _applicationId = Guid.NewGuid();
    private readonly Guid _userProfileId = Guid.NewGuid();
    private readonly Guid _functionalAreaId = Guid.NewGuid();
    private readonly Guid _permissionTypeId = Guid.NewGuid();
    private readonly Guid _permissionId = Guid.NewGuid();
    private readonly Guid _secondPermissionId = Guid.NewGuid();
    private readonly Guid _secondFunctionalAreaId = Guid.NewGuid();
    private readonly Guid _groupId = Guid.NewGuid();
    private readonly Guid _groupPermissionId = Guid.NewGuid();
    private readonly Guid _thirdFunctionalAreaId = Guid.NewGuid();
    private readonly Guid _thirdPermissionTypeId = Guid.NewGuid();

    private const string FunctionalAreaCode = "ORDERS";
    private const string PermissionTypeCode = "READ";
    private const string PermissionCode = "ORDERS_READ";
    private const string SecondPermissionCode = "REPORTS_READ";
    private const string GroupPermissionCode = "INVENTORY_MANAGE";
    private const string ExternalSubjectId = "ext-user-12345";
    private const string IdentityProviderName = "test-idp";

    public AccessCheckEndpointTests(HeimdallWebApplicationFactory factory) : base(factory)
    {
        _tenantId = factory.DefaultTenantId;
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        await SeedTestDataAsync();
    }

    #region Single Permission Check by FunctionalAreaCode + PermissionTypeCode

    [Fact]
    public async Task CheckPermission_ByFunctionalAreaAndPermissionType_ReturnsAllowed()
    {
        // Arrange
        var request = new
        {
            UserProfileId = _userProfileId,
            FunctionalAreaCode = FunctionalAreaCode,
            PermissionTypeCode = PermissionTypeCode
        };

        // Act
        var response = await Client.PostAsJsonAsync(
            $"/api/tenants/{_tenantId}/applications/{_applicationId}/access-checks", request);

        // Assert
        response.EnsureSuccessStatusCode();
        var envelope = await DeserializeEnvelope<PermissionCheckResult>(response);

        Assert.NotNull(envelope);
        Assert.True(envelope.Success);
        Assert.NotNull(envelope.Data);
        Assert.True(envelope.Data.Allowed);
        Assert.Equal("Allow", envelope.Data.Decision);
    }

    [Fact]
    public async Task CheckPermission_ByFunctionalAreaAndPermissionType_NoAssignment_ReturnsDenied()
    {
        // Arrange - use a permission type that the user has no assignment for
        var request = new
        {
            UserProfileId = _userProfileId,
            FunctionalAreaCode = FunctionalAreaCode,
            PermissionTypeCode = "NONEXISTENT_TYPE"
        };

        // Act
        var response = await Client.PostAsJsonAsync(
            $"/api/tenants/{_tenantId}/applications/{_applicationId}/access-checks", request);

        // Assert
        response.EnsureSuccessStatusCode();
        var envelope = await DeserializeEnvelope<PermissionCheckResult>(response);

        Assert.NotNull(envelope);
        Assert.True(envelope.Success);
        Assert.NotNull(envelope.Data);
        Assert.False(envelope.Data.Allowed);
        Assert.Equal("Deny", envelope.Data.Decision);
    }

    #endregion

    #region Single Permission Check by PermissionCode

    [Fact]
    public async Task CheckPermission_ByPermissionCode_ReturnsAllowed()
    {
        // Arrange
        var request = new
        {
            UserProfileId = _userProfileId,
            PermissionCode = PermissionCode
        };

        // Act
        var response = await Client.PostAsJsonAsync(
            $"/api/tenants/{_tenantId}/applications/{_applicationId}/access-checks", request);

        // Assert
        response.EnsureSuccessStatusCode();
        var envelope = await DeserializeEnvelope<PermissionCheckResult>(response);

        Assert.NotNull(envelope);
        Assert.True(envelope.Success);
        Assert.NotNull(envelope.Data);
        Assert.True(envelope.Data.Allowed);
        Assert.Equal("Allow", envelope.Data.Decision);
        Assert.NotEmpty(envelope.Data.MatchedAssignments);
    }

    [Fact]
    public async Task CheckPermission_ByPermissionCode_NonexistentCode_ReturnsDenied()
    {
        // Arrange
        var request = new
        {
            UserProfileId = _userProfileId,
            PermissionCode = "NONEXISTENT_PERMISSION"
        };

        // Act
        var response = await Client.PostAsJsonAsync(
            $"/api/tenants/{_tenantId}/applications/{_applicationId}/access-checks", request);

        // Assert
        response.EnsureSuccessStatusCode();
        var envelope = await DeserializeEnvelope<PermissionCheckResult>(response);

        Assert.NotNull(envelope);
        Assert.True(envelope.Success);
        Assert.NotNull(envelope.Data);
        Assert.False(envelope.Data.Allowed);
        Assert.Equal("Deny", envelope.Data.Decision);
    }

    #endregion

    #region Single Permission Check by ExternalSubjectId + IdentityProvider

    [Fact]
    public async Task CheckPermission_ByExternalSubjectId_ReturnsAllowed()
    {
        // Arrange
        var request = new
        {
            ExternalSubjectId = ExternalSubjectId,
            IdentityProvider = IdentityProviderName,
            PermissionCode = PermissionCode
        };

        // Act
        var response = await Client.PostAsJsonAsync(
            $"/api/tenants/{_tenantId}/applications/{_applicationId}/access-checks", request);

        // Assert
        response.EnsureSuccessStatusCode();
        var envelope = await DeserializeEnvelope<PermissionCheckResult>(response);

        Assert.NotNull(envelope);
        Assert.True(envelope.Success);
        Assert.NotNull(envelope.Data);
        Assert.True(envelope.Data.Allowed);
        Assert.Equal("Allow", envelope.Data.Decision);
    }

    [Fact]
    public async Task CheckPermission_ByExternalSubjectId_WithFunctionalAreaAndPermissionType_ReturnsAllowed()
    {
        // Arrange
        var request = new
        {
            ExternalSubjectId = ExternalSubjectId,
            IdentityProvider = IdentityProviderName,
            FunctionalAreaCode = FunctionalAreaCode,
            PermissionTypeCode = PermissionTypeCode
        };

        // Act
        var response = await Client.PostAsJsonAsync(
            $"/api/tenants/{_tenantId}/applications/{_applicationId}/access-checks", request);

        // Assert
        response.EnsureSuccessStatusCode();
        var envelope = await DeserializeEnvelope<PermissionCheckResult>(response);

        Assert.NotNull(envelope);
        Assert.True(envelope.Success);
        Assert.NotNull(envelope.Data);
        Assert.True(envelope.Data.Allowed);
        Assert.Equal("Allow", envelope.Data.Decision);
    }

    [Fact]
    public async Task CheckPermission_ByExternalSubjectId_NonexistentUser_ReturnsDenied()
    {
        // Arrange
        var request = new
        {
            ExternalSubjectId = "nonexistent-subject",
            IdentityProvider = IdentityProviderName,
            PermissionCode = PermissionCode
        };

        // Act
        var response = await Client.PostAsJsonAsync(
            $"/api/tenants/{_tenantId}/applications/{_applicationId}/access-checks", request);

        // Assert
        response.EnsureSuccessStatusCode();
        var envelope = await DeserializeEnvelope<PermissionCheckResult>(response);

        Assert.NotNull(envelope);
        Assert.True(envelope.Success);
        Assert.NotNull(envelope.Data);
        Assert.False(envelope.Data.Allowed);
        Assert.Equal("Deny", envelope.Data.Decision);
    }

    #endregion

    #region Batch Permission Check

    [Fact]
    public async Task CheckBatch_MultiplePermissions_ReturnsIndividualResults()
    {
        // Arrange
        var request = new
        {
            UserProfileId = _userProfileId,
            Checks = new[]
            {
                new { PermissionCode = PermissionCode },
                new { PermissionCode = SecondPermissionCode },
                new { PermissionCode = "NONEXISTENT_PERM" }
            }
        };

        // Act
        var response = await Client.PostAsJsonAsync(
            $"/api/tenants/{_tenantId}/applications/{_applicationId}/access-checks/batch", request);

        // Assert
        response.EnsureSuccessStatusCode();
        var envelope = await DeserializeEnvelope<BatchPermissionCheckResult>(response);

        Assert.NotNull(envelope);
        Assert.True(envelope.Success);
        Assert.NotNull(envelope.Data);
        Assert.Equal(3, envelope.Data.Results.Count);

        // First permission: user has direct Allow assignment
        Assert.True(envelope.Data.Results[0].Allowed);
        Assert.Equal("Allow", envelope.Data.Results[0].Decision);

        // Second permission: user has direct Allow assignment
        Assert.True(envelope.Data.Results[1].Allowed);
        Assert.Equal("Allow", envelope.Data.Results[1].Decision);

        // Third permission: nonexistent, should be Deny
        Assert.False(envelope.Data.Results[2].Allowed);
        Assert.Equal("Deny", envelope.Data.Results[2].Decision);
    }

    [Fact]
    public async Task CheckBatch_ByFunctionalAreaAndPermissionType_ReturnsResults()
    {
        // Arrange
        var request = new
        {
            UserProfileId = _userProfileId,
            Checks = new[]
            {
                new { FunctionalAreaCode = FunctionalAreaCode, PermissionTypeCode = PermissionTypeCode, PermissionCode = (string?)null },
                new { FunctionalAreaCode = "NONEXISTENT_FA", PermissionTypeCode = "READ", PermissionCode = (string?)null }
            }
        };

        // Act
        var response = await Client.PostAsJsonAsync(
            $"/api/tenants/{_tenantId}/applications/{_applicationId}/access-checks/batch", request);

        // Assert
        response.EnsureSuccessStatusCode();
        var envelope = await DeserializeEnvelope<BatchPermissionCheckResult>(response);

        Assert.NotNull(envelope);
        Assert.True(envelope.Success);
        Assert.NotNull(envelope.Data);
        Assert.Equal(2, envelope.Data.Results.Count);

        // First check: valid FA+PT with assignment → Allow
        Assert.True(envelope.Data.Results[0].Allowed);

        // Second check: nonexistent FA → Deny
        Assert.False(envelope.Data.Results[1].Allowed);
    }

    [Fact]
    public async Task CheckBatch_EmptyChecks_ReturnsBadRequest()
    {
        // Arrange
        var request = new
        {
            UserProfileId = _userProfileId,
            Checks = Array.Empty<object>()
        };

        // Act
        var response = await Client.PostAsJsonAsync(
            $"/api/tenants/{_tenantId}/applications/{_applicationId}/access-checks/batch", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CheckBatch_ExceedsMaximum50_ReturnsBadRequest()
    {
        // Arrange
        var checks = Enumerable.Range(0, 51)
            .Select(i => new { PermissionCode = $"PERM_{i}" })
            .ToArray();

        var request = new
        {
            UserProfileId = _userProfileId,
            Checks = checks
        };

        // Act
        var response = await Client.PostAsJsonAsync(
            $"/api/tenants/{_tenantId}/applications/{_applicationId}/access-checks/batch", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #endregion

    #region Effective Permissions Retrieval

    [Fact]
    public async Task GetEffectivePermissions_ReturnsAllResolvedPermissions()
    {
        // Act
        var response = await Client.GetAsync(
            $"/api/tenants/{_tenantId}/applications/{_applicationId}/access-checks/effective/{_userProfileId}");

        // Assert
        response.EnsureSuccessStatusCode();
        var envelope = await DeserializeEnvelope<EffectivePermissionsResult>(response);

        Assert.NotNull(envelope);
        Assert.True(envelope.Success);
        Assert.NotNull(envelope.Data);
        Assert.NotNull(envelope.Data.Permissions);

        // User has direct assignments and group-inherited assignments
        Assert.NotEmpty(envelope.Data.Permissions);

        // Verify at least the directly assigned permissions are present
        var directPermission = envelope.Data.Permissions
            .FirstOrDefault(p => p.PermissionCode == PermissionCode);
        Assert.NotNull(directPermission);
        Assert.Equal("Allow", directPermission.Effect);

        var secondPermission = envelope.Data.Permissions
            .FirstOrDefault(p => p.PermissionCode == SecondPermissionCode);
        Assert.NotNull(secondPermission);
        Assert.Equal("Allow", secondPermission.Effect);

        // Verify group-inherited permission is also present
        var groupPermission = envelope.Data.Permissions
            .FirstOrDefault(p => p.PermissionCode == GroupPermissionCode);
        Assert.NotNull(groupPermission);
        Assert.Equal("Allow", groupPermission.Effect);
    }

    [Fact]
    public async Task GetEffectivePermissions_NonexistentUser_ReturnsDeniedOrEmpty()
    {
        // Arrange
        var nonexistentUserId = Guid.NewGuid();

        // Act
        var response = await Client.GetAsync(
            $"/api/tenants/{_tenantId}/applications/{_applicationId}/access-checks/effective/{nonexistentUserId}");

        // Assert
        response.EnsureSuccessStatusCode();
        var envelope = await DeserializeEnvelope<EffectivePermissionsResult>(response);

        Assert.NotNull(envelope);
        Assert.True(envelope.Success);
        Assert.NotNull(envelope.Data);
        // No permissions for a nonexistent user
        Assert.Empty(envelope.Data.Permissions);
    }

    #endregion

    #region Validation Tests

    [Fact]
    public async Task CheckPermission_MissingUserProfileIdAndExternalSubjectId_ReturnsBadRequest()
    {
        // Arrange - neither UserProfileId nor ExternalSubjectId provided
        var request = new
        {
            UserProfileId = Guid.Empty,
            PermissionCode = PermissionCode
        };

        // Act
        var response = await Client.PostAsJsonAsync(
            $"/api/tenants/{_tenantId}/applications/{_applicationId}/access-checks", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CheckPermission_MissingPermissionIdentifier_ReturnsBadRequest()
    {
        // Arrange - has UserProfileId but no way to identify the permission
        var request = new
        {
            UserProfileId = _userProfileId
        };

        // Act
        var response = await Client.PostAsJsonAsync(
            $"/api/tenants/{_tenantId}/applications/{_applicationId}/access-checks", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #endregion

    #region Test Helpers

    private async Task SeedTestDataAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<HeimdallDbContext>();
        dbContext.SuppressAutoTimestamps = true;

        var now = DateTime.UtcNow;

        // 1. Create Tenant (check if exists from factory setup)
        if (!await dbContext.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Id == _tenantId))
        {
            var tenant = new Tenant
            {
                Name = "Access Check Test Tenant",
                Slug = $"access-test-{_tenantId.ToString()[..8]}",
                PrimaryIdentityMode = PrimaryIdentityMode.EntraExternalId,
                Status = TenantStatus.Active,
                CreatedAt = now,
                CreatedBy = "test-seed"
            };
            SetEntityId(dbContext, tenant, _tenantId);
        }

        // 2. Create Application
        var application = new Domain.Entities.Application
        {
            TenantId = _tenantId,
            Name = "Access Check Test App",
            ClientIdentifier = $"access-test-client-{_applicationId.ToString()[..8]}",
            Status = ApplicationStatus.Active,
            CreatedAt = now,
            CreatedBy = "test-seed"
        };
        SetEntityId(dbContext, application, _applicationId);

        // 3. Create UserProfile
        var userProfile = new UserProfile
        {
            TenantId = _tenantId,
            ExternalSubjectId = ExternalSubjectId,
            IdentityProvider = IdentityProviderName,
            Email = "testuser@accesscheck.dev",
            DisplayName = "Access Check Test User",
            Status = UserStatus.Active,
            CreatedAt = now,
            CreatedBy = "test-seed"
        };
        SetEntityId(dbContext, userProfile, _userProfileId);

        // 4. Create Functional Areas
        var functionalArea = new FunctionalArea
        {
            TenantId = _tenantId,
            ApplicationId = _applicationId,
            FunctionalAreaCode = FunctionalAreaCode,
            Name = "Orders",
            IsActive = true,
            CreatedAt = now,
            CreatedBy = "test-seed"
        };
        SetEntityId(dbContext, functionalArea, _functionalAreaId);

        var secondFunctionalArea = new FunctionalArea
        {
            TenantId = _tenantId,
            ApplicationId = _applicationId,
            FunctionalAreaCode = "REPORTS",
            Name = "Reports",
            IsActive = true,
            CreatedAt = now,
            CreatedBy = "test-seed"
        };
        SetEntityId(dbContext, secondFunctionalArea, _secondFunctionalAreaId);

        var thirdFunctionalArea = new FunctionalArea
        {
            TenantId = _tenantId,
            ApplicationId = _applicationId,
            FunctionalAreaCode = "INVENTORY",
            Name = "Inventory",
            IsActive = true,
            CreatedAt = now,
            CreatedBy = "test-seed"
        };
        SetEntityId(dbContext, thirdFunctionalArea, _thirdFunctionalAreaId);

        // 5. Create Permission Types
        var permissionType = new PermissionType
        {
            TenantId = _tenantId,
            ApplicationId = _applicationId,
            Code = PermissionTypeCode,
            Name = "Read",
            IsActive = true,
            CreatedAt = now,
            CreatedBy = "test-seed"
        };
        SetEntityId(dbContext, permissionType, _permissionTypeId);

        var thirdPermissionType = new PermissionType
        {
            TenantId = _tenantId,
            ApplicationId = _applicationId,
            Code = "MANAGE",
            Name = "Manage",
            IsActive = true,
            CreatedAt = now,
            CreatedBy = "test-seed"
        };
        SetEntityId(dbContext, thirdPermissionType, _thirdPermissionTypeId);

        // 6. Create Permissions
        var permission = new Permission
        {
            TenantId = _tenantId,
            ApplicationId = _applicationId,
            FunctionalAreaId = _functionalAreaId,
            PermissionTypeId = _permissionTypeId,
            PermissionCode = PermissionCode,
            Name = "Orders Read",
            IsActive = true,
            CreatedAt = now,
            CreatedBy = "test-seed"
        };
        SetEntityId(dbContext, permission, _permissionId);

        var secondPermission = new Permission
        {
            TenantId = _tenantId,
            ApplicationId = _applicationId,
            FunctionalAreaId = _secondFunctionalAreaId,
            PermissionTypeId = _permissionTypeId,
            PermissionCode = SecondPermissionCode,
            Name = "Reports Read",
            IsActive = true,
            CreatedAt = now,
            CreatedBy = "test-seed"
        };
        SetEntityId(dbContext, secondPermission, _secondPermissionId);

        var groupPermission = new Permission
        {
            TenantId = _tenantId,
            ApplicationId = _applicationId,
            FunctionalAreaId = _thirdFunctionalAreaId,
            PermissionTypeId = _thirdPermissionTypeId,
            PermissionCode = GroupPermissionCode,
            Name = "Inventory Manage",
            IsActive = true,
            CreatedAt = now,
            CreatedBy = "test-seed"
        };
        SetEntityId(dbContext, groupPermission, _groupPermissionId);

        // 7. Create Group
        var group = new Group
        {
            TenantId = _tenantId,
            ApplicationId = _applicationId,
            Name = "Inventory Managers",
            IsActive = true,
            CreatedAt = now,
            CreatedBy = "test-seed"
        };
        SetEntityId(dbContext, group, _groupId);

        // 8. Create Group Membership (user belongs to group)
        var membership = new GroupMembership
        {
            TenantId = _tenantId,
            ApplicationId = _applicationId,
            GroupId = _groupId,
            UserProfileId = _userProfileId,
            CreatedAt = now,
            CreatedBy = "test-seed"
        };
        SetEntityId(dbContext, membership, Guid.NewGuid());

        // 9. Create Direct User Permission Assignments
        var directAssignment1 = new UserPermissionAssignment
        {
            TenantId = _tenantId,
            ApplicationId = _applicationId,
            UserProfileId = _userProfileId,
            PermissionId = _permissionId,
            Effect = Effect.Allow,
            ValidFrom = null,
            ValidTo = null,
            CreatedAt = now,
            CreatedBy = "test-seed"
        };
        SetEntityId(dbContext, directAssignment1, Guid.NewGuid());

        var directAssignment2 = new UserPermissionAssignment
        {
            TenantId = _tenantId,
            ApplicationId = _applicationId,
            UserProfileId = _userProfileId,
            PermissionId = _secondPermissionId,
            Effect = Effect.Allow,
            ValidFrom = null,
            ValidTo = null,
            CreatedAt = now,
            CreatedBy = "test-seed"
        };
        SetEntityId(dbContext, directAssignment2, Guid.NewGuid());

        // 10. Create Group Permission Assignment (inherited by user via group)
        var groupAssignment = new GroupPermissionAssignment
        {
            TenantId = _tenantId,
            ApplicationId = _applicationId,
            GroupId = _groupId,
            PermissionId = _groupPermissionId,
            Effect = Effect.Allow,
            ValidFrom = null,
            ValidTo = null,
            CreatedAt = now,
            CreatedBy = "test-seed"
        };
        SetEntityId(dbContext, groupAssignment, Guid.NewGuid());

        await dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Sets the Id property on a BaseEntity via EF Core's change tracker,
    /// bypassing the protected setter.
    /// </summary>
    private static void SetEntityId<T>(HeimdallDbContext dbContext, T entity, Guid id) where T : BaseEntity
    {
        dbContext.Add(entity);
        dbContext.Entry(entity).Property(e => e.Id).CurrentValue = id;
    }

    private static async Task<ApiEnvelope<T>?> DeserializeEnvelope<T>(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<ApiEnvelope<T>>(content, JsonOptions);
    }

    #endregion
}
