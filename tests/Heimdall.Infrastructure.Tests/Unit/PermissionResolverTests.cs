using Heimdall.Domain.Entities;
using Heimdall.Domain.Enums;
using Heimdall.Domain.Interfaces;
using Heimdall.Infrastructure.Logging;
using Heimdall.Infrastructure.Persistence;
using Heimdall.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Heimdall.Infrastructure.Tests.Unit;

/// <summary>
/// Unit tests for PermissionResolver using in-memory EF Core DbContext.
/// Validates: Requirements 27.1
/// </summary>
public class PermissionResolverTests : IDisposable
{
    private readonly HeimdallDbContext _db;
    private readonly PermissionResolver _resolver;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _applicationId = Guid.NewGuid();

    public PermissionResolverTests()
    {
        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(_tenantId);
        tenantContext.ActorEmail.Returns("test@heimdall.dev");

        var options = new DbContextOptionsBuilder<HeimdallDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new HeimdallDbContext(options, tenantContext);
        _resolver = new PermissionResolver(_db, NullHeimdallMetricsService.Instance);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    #region Helpers

    private UserProfile CreateActiveUser()
    {
        var user = new UserProfile
        {
            TenantId = _tenantId,
            ExternalSubjectId = $"ext-{Guid.NewGuid()}",
            IdentityProvider = "TestIdP",
            Email = "user@test.com",
            DisplayName = "Test User",
            Status = UserStatus.Active
        };
        _db.UserProfiles.Add(user);
        return user;
    }

    private FunctionalArea CreateFunctionalArea(bool isActive = true)
    {
        var fa = new FunctionalArea
        {
            TenantId = _tenantId,
            ApplicationId = _applicationId,
            FunctionalAreaCode = $"FA_{Guid.NewGuid():N}"[..20].ToUpper(),
            Name = "Test Functional Area",
            IsActive = isActive
        };
        _db.FunctionalAreas.Add(fa);
        return fa;
    }

    private PermissionType CreatePermissionType(bool isActive = true)
    {
        var pt = new PermissionType
        {
            TenantId = _tenantId,
            ApplicationId = _applicationId,
            Code = $"PT_{Guid.NewGuid():N}"[..20].ToUpper(),
            Name = "Test Permission Type",
            IsActive = isActive
        };
        _db.PermissionTypes.Add(pt);
        return pt;
    }

    private Permission CreatePermission(Guid functionalAreaId, Guid permissionTypeId, bool isActive = true)
    {
        var perm = new Permission
        {
            TenantId = _tenantId,
            ApplicationId = _applicationId,
            FunctionalAreaId = functionalAreaId,
            PermissionTypeId = permissionTypeId,
            PermissionCode = $"PERM_{Guid.NewGuid():N}"[..30].ToUpper(),
            Name = "Test Permission",
            IsActive = isActive
        };
        _db.Permissions.Add(perm);
        return perm;
    }

    private Group CreateGroup(bool isActive = true)
    {
        var group = new Group
        {
            TenantId = _tenantId,
            ApplicationId = _applicationId,
            Name = $"Group-{Guid.NewGuid():N}"[..20],
            IsActive = isActive
        };
        _db.Groups.Add(group);
        return group;
    }

    private GroupMembership CreateGroupMembership(Guid groupId, Guid userProfileId)
    {
        var membership = new GroupMembership
        {
            TenantId = _tenantId,
            ApplicationId = _applicationId,
            GroupId = groupId,
            UserProfileId = userProfileId
        };
        _db.GroupMemberships.Add(membership);
        return membership;
    }

    private UserPermissionAssignment CreateUserAssignment(
        Guid userProfileId, Guid permissionId, Effect effect,
        DateTime? validFrom = null, DateTime? validTo = null)
    {
        var assignment = new UserPermissionAssignment
        {
            TenantId = _tenantId,
            ApplicationId = _applicationId,
            UserProfileId = userProfileId,
            PermissionId = permissionId,
            Effect = effect,
            ValidFrom = validFrom,
            ValidTo = validTo
        };
        _db.UserPermissionAssignments.Add(assignment);
        return assignment;
    }

    private GroupPermissionAssignment CreateGroupAssignment(
        Guid groupId, Guid permissionId, Effect effect,
        DateTime? validFrom = null, DateTime? validTo = null)
    {
        var assignment = new GroupPermissionAssignment
        {
            TenantId = _tenantId,
            ApplicationId = _applicationId,
            GroupId = groupId,
            PermissionId = permissionId,
            Effect = effect,
            ValidFrom = validFrom,
            ValidTo = validTo
        };
        _db.GroupPermissionAssignments.Add(assignment);
        return assignment;
    }

    #endregion

    #region Deny overrides Allow with conflicting assignments

    [Fact]
    public async Task CheckPermission_DenyOverridesAllow_WithConflictingDirectAssignments()
    {
        // Arrange
        var user = CreateActiveUser();
        var fa = CreateFunctionalArea();
        var pt = CreatePermissionType();
        var perm = CreatePermission(fa.Id, pt.Id);

        CreateUserAssignment(user.Id, perm.Id, Effect.Allow);
        CreateUserAssignment(user.Id, perm.Id, Effect.Deny);
        await _db.SaveChangesAsync();

        // Act
        var result = await _resolver.CheckPermissionByCodeAsync(
            _tenantId, _applicationId, user.Id, perm.PermissionCode);

        // Assert
        Assert.False(result.Allowed);
        Assert.Equal("Deny", result.Decision);
    }

    [Fact]
    public async Task CheckPermission_DenyOverridesAllow_WithConflictingGroupAndDirectAssignments()
    {
        // Arrange
        var user = CreateActiveUser();
        var fa = CreateFunctionalArea();
        var pt = CreatePermissionType();
        var perm = CreatePermission(fa.Id, pt.Id);
        var group = CreateGroup();
        CreateGroupMembership(group.Id, user.Id);

        // Direct Allow + Group Deny
        CreateUserAssignment(user.Id, perm.Id, Effect.Allow);
        CreateGroupAssignment(group.Id, perm.Id, Effect.Deny);
        await _db.SaveChangesAsync();

        // Act
        var result = await _resolver.CheckPermissionByCodeAsync(
            _tenantId, _applicationId, user.Id, perm.PermissionCode);

        // Assert
        Assert.False(result.Allowed);
        Assert.Equal("Deny", result.Decision);
    }

    #endregion

    #region Expired permission (outside ValidFrom/ValidTo) excluded

    [Fact]
    public async Task CheckPermission_ExpiredAssignment_IsExcluded()
    {
        // Arrange
        var user = CreateActiveUser();
        var fa = CreateFunctionalArea();
        var pt = CreatePermissionType();
        var perm = CreatePermission(fa.Id, pt.Id);

        // Assignment expired yesterday
        var yesterday = DateTime.UtcNow.AddDays(-1);
        var twoDaysAgo = DateTime.UtcNow.AddDays(-2);
        CreateUserAssignment(user.Id, perm.Id, Effect.Allow, twoDaysAgo, yesterday);
        await _db.SaveChangesAsync();

        // Act
        var result = await _resolver.CheckPermissionByCodeAsync(
            _tenantId, _applicationId, user.Id, perm.PermissionCode);

        // Assert — no valid assignments, so Deny
        Assert.False(result.Allowed);
        Assert.Equal("Deny", result.Decision);
    }

    [Fact]
    public async Task CheckPermission_FutureAssignment_IsExcluded()
    {
        // Arrange
        var user = CreateActiveUser();
        var fa = CreateFunctionalArea();
        var pt = CreatePermissionType();
        var perm = CreatePermission(fa.Id, pt.Id);

        // Assignment starts tomorrow
        var tomorrow = DateTime.UtcNow.AddDays(1);
        var nextWeek = DateTime.UtcNow.AddDays(7);
        CreateUserAssignment(user.Id, perm.Id, Effect.Allow, tomorrow, nextWeek);
        await _db.SaveChangesAsync();

        // Act
        var result = await _resolver.CheckPermissionByCodeAsync(
            _tenantId, _applicationId, user.Id, perm.PermissionCode);

        // Assert
        Assert.False(result.Allowed);
        Assert.Equal("Deny", result.Decision);
    }

    [Fact]
    public async Task CheckPermission_ActiveAssignment_WithinWindow_IsIncluded()
    {
        // Arrange
        var user = CreateActiveUser();
        var fa = CreateFunctionalArea();
        var pt = CreatePermissionType();
        var perm = CreatePermission(fa.Id, pt.Id);

        var yesterday = DateTime.UtcNow.AddDays(-1);
        var tomorrow = DateTime.UtcNow.AddDays(1);
        CreateUserAssignment(user.Id, perm.Id, Effect.Allow, yesterday, tomorrow);
        await _db.SaveChangesAsync();

        // Act
        var result = await _resolver.CheckPermissionByCodeAsync(
            _tenantId, _applicationId, user.Id, perm.PermissionCode);

        // Assert
        Assert.True(result.Allowed);
        Assert.Equal("Allow", result.Decision);
    }

    #endregion

    #region Inactive UserProfile → Deny

    [Fact]
    public async Task CheckPermission_InactiveUser_ReturnsDeny()
    {
        // Arrange
        var user = CreateActiveUser();
        user.Status = UserStatus.Inactive;
        var fa = CreateFunctionalArea();
        var pt = CreatePermissionType();
        var perm = CreatePermission(fa.Id, pt.Id);
        CreateUserAssignment(user.Id, perm.Id, Effect.Allow);
        await _db.SaveChangesAsync();

        // Act
        var result = await _resolver.CheckPermissionByCodeAsync(
            _tenantId, _applicationId, user.Id, perm.PermissionCode);

        // Assert
        Assert.False(result.Allowed);
        Assert.Equal("Deny", result.Decision);
        Assert.Contains("inactive", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Inactive FunctionalArea → excluded

    [Fact]
    public async Task CheckPermission_InactiveFunctionalArea_ViaPermissionCode_ReturnsDeny()
    {
        // Arrange
        var user = CreateActiveUser();
        var fa = CreateFunctionalArea(isActive: false);
        var pt = CreatePermissionType();
        var perm = CreatePermission(fa.Id, pt.Id);
        CreateUserAssignment(user.Id, perm.Id, Effect.Allow);
        await _db.SaveChangesAsync();

        // Act
        var result = await _resolver.CheckPermissionByCodeAsync(
            _tenantId, _applicationId, user.Id, perm.PermissionCode);

        // Assert
        Assert.False(result.Allowed);
        Assert.Equal("Deny", result.Decision);
    }

    [Fact]
    public async Task CheckPermission_InactiveFunctionalArea_ViaFAAndPTCode_ReturnsDeny()
    {
        // Arrange
        var user = CreateActiveUser();
        var fa = CreateFunctionalArea(isActive: false);
        var pt = CreatePermissionType();
        var perm = CreatePermission(fa.Id, pt.Id);
        CreateUserAssignment(user.Id, perm.Id, Effect.Allow);
        await _db.SaveChangesAsync();

        // Act
        var result = await _resolver.CheckPermissionAsync(
            _tenantId, _applicationId, user.Id,
            fa.FunctionalAreaCode, pt.Code);

        // Assert
        Assert.False(result.Allowed);
        Assert.Equal("Deny", result.Decision);
    }

    #endregion

    #region Inactive PermissionType → excluded

    [Fact]
    public async Task CheckPermission_InactivePermissionType_ReturnsDeny()
    {
        // Arrange
        var user = CreateActiveUser();
        var fa = CreateFunctionalArea();
        var pt = CreatePermissionType(isActive: false);
        var perm = CreatePermission(fa.Id, pt.Id);
        CreateUserAssignment(user.Id, perm.Id, Effect.Allow);
        await _db.SaveChangesAsync();

        // Act
        var result = await _resolver.CheckPermissionByCodeAsync(
            _tenantId, _applicationId, user.Id, perm.PermissionCode);

        // Assert
        Assert.False(result.Allowed);
        Assert.Equal("Deny", result.Decision);
    }

    #endregion

    #region Inactive Permission → excluded

    [Fact]
    public async Task CheckPermission_InactivePermission_ReturnsDeny()
    {
        // Arrange
        var user = CreateActiveUser();
        var fa = CreateFunctionalArea();
        var pt = CreatePermissionType();
        var perm = CreatePermission(fa.Id, pt.Id, isActive: false);
        CreateUserAssignment(user.Id, perm.Id, Effect.Allow);
        await _db.SaveChangesAsync();

        // Act
        var result = await _resolver.CheckPermissionByCodeAsync(
            _tenantId, _applicationId, user.Id, perm.PermissionCode);

        // Assert
        Assert.False(result.Allowed);
        Assert.Equal("Deny", result.Decision);
    }

    #endregion

    #region Inactive Group → excluded from group inheritance

    [Fact]
    public async Task CheckPermission_InactiveGroup_AssignmentExcludedFromResolution()
    {
        // Arrange
        var user = CreateActiveUser();
        var fa = CreateFunctionalArea();
        var pt = CreatePermissionType();
        var perm = CreatePermission(fa.Id, pt.Id);
        var inactiveGroup = CreateGroup(isActive: false);
        CreateGroupMembership(inactiveGroup.Id, user.Id);
        CreateGroupAssignment(inactiveGroup.Id, perm.Id, Effect.Allow);
        await _db.SaveChangesAsync();

        // Act — only group assignment exists but group is inactive
        var result = await _resolver.CheckPermissionByCodeAsync(
            _tenantId, _applicationId, user.Id, perm.PermissionCode);

        // Assert — no valid assignments
        Assert.False(result.Allowed);
        Assert.Equal("Deny", result.Decision);
    }

    [Fact]
    public async Task CheckPermission_InactiveGroup_DenyNotInheritedFromInactiveGroup()
    {
        // Arrange
        var user = CreateActiveUser();
        var fa = CreateFunctionalArea();
        var pt = CreatePermissionType();
        var perm = CreatePermission(fa.Id, pt.Id);

        // Active group with Allow
        var activeGroup = CreateGroup(isActive: true);
        CreateGroupMembership(activeGroup.Id, user.Id);
        CreateGroupAssignment(activeGroup.Id, perm.Id, Effect.Allow);

        // Inactive group with Deny — should not be considered
        var inactiveGroup = CreateGroup(isActive: false);
        CreateGroupMembership(inactiveGroup.Id, user.Id);
        CreateGroupAssignment(inactiveGroup.Id, perm.Id, Effect.Deny);

        await _db.SaveChangesAsync();

        // Act
        var result = await _resolver.CheckPermissionByCodeAsync(
            _tenantId, _applicationId, user.Id, perm.PermissionCode);

        // Assert — inactive group's Deny is excluded, so Allow from active group wins
        Assert.True(result.Allowed);
        Assert.Equal("Allow", result.Decision);
    }

    #endregion

    #region Group permission inheritance resolves to user

    [Fact]
    public async Task CheckPermission_GroupPermissionInheritance_AllowResolvesToUser()
    {
        // Arrange
        var user = CreateActiveUser();
        var fa = CreateFunctionalArea();
        var pt = CreatePermissionType();
        var perm = CreatePermission(fa.Id, pt.Id);
        var group = CreateGroup();
        CreateGroupMembership(group.Id, user.Id);
        CreateGroupAssignment(group.Id, perm.Id, Effect.Allow);
        await _db.SaveChangesAsync();

        // Act
        var result = await _resolver.CheckPermissionByCodeAsync(
            _tenantId, _applicationId, user.Id, perm.PermissionCode);

        // Assert
        Assert.True(result.Allowed);
        Assert.Equal("Allow", result.Decision);
        Assert.Single(result.MatchedAssignments);
        Assert.Equal("Group", result.MatchedAssignments[0].Source);
        Assert.Equal(group.Id, result.MatchedAssignments[0].GroupId);
    }

    [Fact]
    public async Task CheckPermission_MultipleGroupInheritance_CombinesAssignments()
    {
        // Arrange
        var user = CreateActiveUser();
        var fa = CreateFunctionalArea();
        var pt = CreatePermissionType();
        var perm = CreatePermission(fa.Id, pt.Id);

        var group1 = CreateGroup();
        var group2 = CreateGroup();
        CreateGroupMembership(group1.Id, user.Id);
        CreateGroupMembership(group2.Id, user.Id);
        CreateGroupAssignment(group1.Id, perm.Id, Effect.Allow);
        CreateGroupAssignment(group2.Id, perm.Id, Effect.Allow);
        await _db.SaveChangesAsync();

        // Act
        var result = await _resolver.CheckPermissionByCodeAsync(
            _tenantId, _applicationId, user.Id, perm.PermissionCode);

        // Assert
        Assert.True(result.Allowed);
        Assert.Equal(2, result.MatchedAssignments.Count);
        Assert.All(result.MatchedAssignments, a => Assert.Equal("Group", a.Source));
    }

    #endregion

    #region Direct user permission assignment resolves correctly

    [Fact]
    public async Task CheckPermission_DirectUserAssignment_Allow_ReturnsAllow()
    {
        // Arrange
        var user = CreateActiveUser();
        var fa = CreateFunctionalArea();
        var pt = CreatePermissionType();
        var perm = CreatePermission(fa.Id, pt.Id);
        CreateUserAssignment(user.Id, perm.Id, Effect.Allow);
        await _db.SaveChangesAsync();

        // Act
        var result = await _resolver.CheckPermissionByCodeAsync(
            _tenantId, _applicationId, user.Id, perm.PermissionCode);

        // Assert
        Assert.True(result.Allowed);
        Assert.Equal("Allow", result.Decision);
        Assert.Single(result.MatchedAssignments);
        Assert.Equal("DirectUser", result.MatchedAssignments[0].Source);
    }

    [Fact]
    public async Task CheckPermission_DirectUserAssignment_Deny_ReturnsDeny()
    {
        // Arrange
        var user = CreateActiveUser();
        var fa = CreateFunctionalArea();
        var pt = CreatePermissionType();
        var perm = CreatePermission(fa.Id, pt.Id);
        CreateUserAssignment(user.Id, perm.Id, Effect.Deny);
        await _db.SaveChangesAsync();

        // Act
        var result = await _resolver.CheckPermissionByCodeAsync(
            _tenantId, _applicationId, user.Id, perm.PermissionCode);

        // Assert
        Assert.False(result.Allowed);
        Assert.Equal("Deny", result.Decision);
    }

    [Fact]
    public async Task CheckPermission_NoAssignments_ReturnsDenyByDefault()
    {
        // Arrange
        var user = CreateActiveUser();
        var fa = CreateFunctionalArea();
        var pt = CreatePermissionType();
        var perm = CreatePermission(fa.Id, pt.Id);
        // No assignments created
        await _db.SaveChangesAsync();

        // Act
        var result = await _resolver.CheckPermissionByCodeAsync(
            _tenantId, _applicationId, user.Id, perm.PermissionCode);

        // Assert
        Assert.False(result.Allowed);
        Assert.Equal("Deny", result.Decision);
    }

    #endregion

    #region CheckPermissionAsync (by FA code + PT code)

    [Fact]
    public async Task CheckPermissionAsync_ByFAAndPTCode_ResolvesCorrectly()
    {
        // Arrange
        var user = CreateActiveUser();
        var fa = CreateFunctionalArea();
        var pt = CreatePermissionType();
        var perm = CreatePermission(fa.Id, pt.Id);
        CreateUserAssignment(user.Id, perm.Id, Effect.Allow);
        await _db.SaveChangesAsync();

        // Act
        var result = await _resolver.CheckPermissionAsync(
            _tenantId, _applicationId, user.Id,
            fa.FunctionalAreaCode, pt.Code);

        // Assert
        Assert.True(result.Allowed);
        Assert.Equal("Allow", result.Decision);
    }

    #endregion

    #region Null ValidFrom/ValidTo treated as unbounded

    [Fact]
    public async Task CheckPermission_NullValidFrom_TreatedAsValidFromBeginningOfTime()
    {
        // Arrange
        var user = CreateActiveUser();
        var fa = CreateFunctionalArea();
        var pt = CreatePermissionType();
        var perm = CreatePermission(fa.Id, pt.Id);
        CreateUserAssignment(user.Id, perm.Id, Effect.Allow, validFrom: null, validTo: DateTime.UtcNow.AddDays(1));
        await _db.SaveChangesAsync();

        // Act
        var result = await _resolver.CheckPermissionByCodeAsync(
            _tenantId, _applicationId, user.Id, perm.PermissionCode);

        // Assert
        Assert.True(result.Allowed);
    }

    [Fact]
    public async Task CheckPermission_NullValidTo_TreatedAsValidIndefinitely()
    {
        // Arrange
        var user = CreateActiveUser();
        var fa = CreateFunctionalArea();
        var pt = CreatePermissionType();
        var perm = CreatePermission(fa.Id, pt.Id);
        CreateUserAssignment(user.Id, perm.Id, Effect.Allow, validFrom: DateTime.UtcNow.AddDays(-1), validTo: null);
        await _db.SaveChangesAsync();

        // Act
        var result = await _resolver.CheckPermissionByCodeAsync(
            _tenantId, _applicationId, user.Id, perm.PermissionCode);

        // Assert
        Assert.True(result.Allowed);
    }

    #endregion
}
