using Heimdall.Infrastructure.Logging;
using FsCheck;
using FsCheck.Xunit;
using Heimdall.Domain.Entities;
using Heimdall.Domain.Enums;
using Heimdall.Domain.Interfaces;
using Heimdall.Infrastructure.Persistence;
using Heimdall.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Heimdall.Infrastructure.Tests.Properties;

/// <summary>
/// Property 3: Inactive Entity Exclusion
/// 
/// Validates: Requirements 5.6, 8.8, 10.4
/// 
/// When any entity in the permission chain (FunctionalArea, PermissionType, Permission, or Group)
/// is inactive, the permission resolver must exclude those assignments from evaluation
/// and return a Deny decision.
/// </summary>
public class PermissionResolver_InactiveEntityExclusionTests : IDisposable
{
    private readonly ITenantContext _tenantContext;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _applicationId = Guid.NewGuid();
    private readonly HeimdallDbContext _db;
    private readonly PermissionResolver _resolver;

    public PermissionResolver_InactiveEntityExclusionTests()
    {
        _tenantContext = Substitute.For<ITenantContext>();
        _tenantContext.TenantId.Returns(_tenantId);
        _tenantContext.ActorEmail.Returns("test@test.com");

        var options = new DbContextOptionsBuilder<HeimdallDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new HeimdallDbContext(options, _tenantContext);
        _resolver = new PermissionResolver(_db, NullHeimdallMetricsService.Instance);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    /// <summary>
    /// **Validates: Requirements 5.6, 10.4**
    /// When FunctionalArea.IsActive is false, permission check returns Deny regardless of valid assignments.
    /// </summary>
    [Property]
    public async Task<bool> InactiveFunctionalArea_AlwaysReturnsDeny(
        bool assignmentIsAllow)
    {
        // Arrange - create a full permission chain with inactive FunctionalArea
        var userId = Guid.NewGuid();
        var faId = Guid.NewGuid();
        var ptId = Guid.NewGuid();
        var permId = Guid.NewGuid();
        var faCode = $"FA_{Guid.NewGuid():N}"[..20].ToUpper();
        var ptCode = $"PT_{Guid.NewGuid():N}"[..20].ToUpper();

        await SeedActiveUser(userId);
        await SeedFunctionalArea(faId, faCode, isActive: false); // INACTIVE
        await SeedPermissionType(ptId, ptCode, isActive: true);
        await SeedPermission(permId, faId, ptId, isActive: true);
        await SeedDirectAssignment(userId, permId, assignmentIsAllow ? Effect.Allow : Effect.Deny);

        // Act
        var result = await _resolver.CheckPermissionAsync(
            _tenantId, _applicationId, userId, faCode, ptCode);

        // Assert - must always be Deny when FA is inactive
        return result.Allowed == false && result.Decision == "Deny";
    }

    /// <summary>
    /// **Validates: Requirements 10.4**
    /// When PermissionType.IsActive is false, permission check returns Deny regardless of valid assignments.
    /// </summary>
    [Property]
    public async Task<bool> InactivePermissionType_AlwaysReturnsDeny(
        bool assignmentIsAllow)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var faId = Guid.NewGuid();
        var ptId = Guid.NewGuid();
        var permId = Guid.NewGuid();
        var faCode = $"FA_{Guid.NewGuid():N}"[..20].ToUpper();
        var ptCode = $"PT_{Guid.NewGuid():N}"[..20].ToUpper();

        await SeedActiveUser(userId);
        await SeedFunctionalArea(faId, faCode, isActive: true);
        await SeedPermissionType(ptId, ptCode, isActive: false); // INACTIVE
        await SeedPermission(permId, faId, ptId, isActive: true);
        await SeedDirectAssignment(userId, permId, assignmentIsAllow ? Effect.Allow : Effect.Deny);

        // Act
        var result = await _resolver.CheckPermissionAsync(
            _tenantId, _applicationId, userId, faCode, ptCode);

        // Assert
        return result.Allowed == false && result.Decision == "Deny";
    }

    /// <summary>
    /// **Validates: Requirements 10.4**
    /// When Permission.IsActive is false, permission check returns Deny regardless of valid assignments.
    /// </summary>
    [Property]
    public async Task<bool> InactivePermission_AlwaysReturnsDeny(
        bool assignmentIsAllow)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var faId = Guid.NewGuid();
        var ptId = Guid.NewGuid();
        var permId = Guid.NewGuid();
        var faCode = $"FA_{Guid.NewGuid():N}"[..20].ToUpper();
        var ptCode = $"PT_{Guid.NewGuid():N}"[..20].ToUpper();

        await SeedActiveUser(userId);
        await SeedFunctionalArea(faId, faCode, isActive: true);
        await SeedPermissionType(ptId, ptCode, isActive: true);
        await SeedPermission(permId, faId, ptId, isActive: false); // INACTIVE
        await SeedDirectAssignment(userId, permId, assignmentIsAllow ? Effect.Allow : Effect.Deny);

        // Act
        var result = await _resolver.CheckPermissionAsync(
            _tenantId, _applicationId, userId, faCode, ptCode);

        // Assert
        return result.Allowed == false && result.Decision == "Deny";
    }

    /// <summary>
    /// **Validates: Requirements 8.8, 10.4**
    /// When a Group is inactive, group permission assignments from that group are excluded
    /// from evaluation. If no other valid assignments exist, result is Deny.
    /// </summary>
    [Property]
    public async Task<bool> InactiveGroup_ExcludesGroupAssignmentsFromEvaluation(
        bool assignmentIsAllow)
    {
        // Arrange - user belongs to an inactive group that has an Allow assignment
        var userId = Guid.NewGuid();
        var faId = Guid.NewGuid();
        var ptId = Guid.NewGuid();
        var permId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var faCode = $"FA_{Guid.NewGuid():N}"[..20].ToUpper();
        var ptCode = $"PT_{Guid.NewGuid():N}"[..20].ToUpper();

        await SeedActiveUser(userId);
        await SeedFunctionalArea(faId, faCode, isActive: true);
        await SeedPermissionType(ptId, ptCode, isActive: true);
        await SeedPermission(permId, faId, ptId, isActive: true);
        await SeedGroup(groupId, isActive: false); // INACTIVE group
        await SeedGroupMembership(groupId, userId);
        await SeedGroupAssignment(groupId, permId, assignmentIsAllow ? Effect.Allow : Effect.Deny);

        // Act
        var result = await _resolver.CheckPermissionAsync(
            _tenantId, _applicationId, userId, faCode, ptCode);

        // Assert - inactive group assignments are excluded, so no assignments remain → Deny
        return result.Allowed == false && result.Decision == "Deny";
    }

    #region Seed Helpers

    private async Task SeedActiveUser(Guid userId)
    {
        var user = new UserProfile
        {
            TenantId = _tenantId,
            ExternalSubjectId = $"ext_{userId:N}",
            IdentityProvider = "TestIdP",
            Email = $"user_{userId:N}@test.com",
            DisplayName = "Test User",
            Status = UserStatus.Active
        };
        // Use EF Core's entry to set the protected Id
        _db.UserProfiles.Add(user);
        _db.Entry(user).Property(e => e.Id).CurrentValue = userId;
        await _db.SaveChangesAsync();
    }

    private async Task SeedFunctionalArea(Guid faId, string code, bool isActive)
    {
        var fa = new FunctionalArea
        {
            TenantId = _tenantId,
            ApplicationId = _applicationId,
            FunctionalAreaCode = code,
            Name = $"FA {code}",
            IsActive = isActive
        };
        _db.FunctionalAreas.Add(fa);
        _db.Entry(fa).Property(e => e.Id).CurrentValue = faId;
        await _db.SaveChangesAsync();
    }

    private async Task SeedPermissionType(Guid ptId, string code, bool isActive)
    {
        var pt = new PermissionType
        {
            TenantId = _tenantId,
            ApplicationId = _applicationId,
            Code = code,
            Name = $"PT {code}",
            IsActive = isActive
        };
        _db.PermissionTypes.Add(pt);
        _db.Entry(pt).Property(e => e.Id).CurrentValue = ptId;
        await _db.SaveChangesAsync();
    }

    private async Task SeedPermission(Guid permId, Guid faId, Guid ptId, bool isActive)
    {
        var perm = new Permission
        {
            TenantId = _tenantId,
            ApplicationId = _applicationId,
            FunctionalAreaId = faId,
            PermissionTypeId = ptId,
            PermissionCode = $"PERM_{Guid.NewGuid():N}"[..30].ToUpper(),
            Name = "Test Permission",
            IsActive = isActive
        };
        _db.Permissions.Add(perm);
        _db.Entry(perm).Property(e => e.Id).CurrentValue = permId;
        await _db.SaveChangesAsync();
    }

    private async Task SeedGroup(Guid groupId, bool isActive)
    {
        var group = new Group
        {
            TenantId = _tenantId,
            ApplicationId = _applicationId,
            Name = $"Group_{groupId:N}",
            IsActive = isActive
        };
        _db.Groups.Add(group);
        _db.Entry(group).Property(e => e.Id).CurrentValue = groupId;
        await _db.SaveChangesAsync();
    }

    private async Task SeedGroupMembership(Guid groupId, Guid userId)
    {
        var membership = new GroupMembership
        {
            TenantId = _tenantId,
            ApplicationId = _applicationId,
            GroupId = groupId,
            UserProfileId = userId
        };
        _db.GroupMemberships.Add(membership);
        await _db.SaveChangesAsync();
    }

    private async Task SeedDirectAssignment(Guid userId, Guid permId, Effect effect)
    {
        var assignment = new UserPermissionAssignment
        {
            TenantId = _tenantId,
            ApplicationId = _applicationId,
            UserProfileId = userId,
            PermissionId = permId,
            Effect = effect,
            ValidFrom = null,
            ValidTo = null
        };
        _db.UserPermissionAssignments.Add(assignment);
        await _db.SaveChangesAsync();
    }

    private async Task SeedGroupAssignment(Guid groupId, Guid permId, Effect effect)
    {
        var assignment = new GroupPermissionAssignment
        {
            TenantId = _tenantId,
            ApplicationId = _applicationId,
            GroupId = groupId,
            PermissionId = permId,
            Effect = effect,
            ValidFrom = null,
            ValidTo = null
        };
        _db.GroupPermissionAssignments.Add(assignment);
        await _db.SaveChangesAsync();
    }

    #endregion
}
