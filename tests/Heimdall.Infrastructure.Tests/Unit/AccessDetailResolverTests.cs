using Heimdall.Domain.Entities;
using Heimdall.Domain.Enums;
using Heimdall.Domain.Interfaces;
using Heimdall.Infrastructure.Persistence;
using Heimdall.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Heimdall.Infrastructure.Tests.Unit;

/// <summary>
/// Unit tests for AccessDetailResolver covering scenarios specified in Requirement 27.3:
/// - User access details filtered by FA Access Requirement declarations
/// - Group access detail inheritance combines with direct user details
/// - Expired access detail (outside ValidFrom/ValidTo) excluded
/// - Inactive access detail excluded from results
/// </summary>
public class AccessDetailResolverTests : IDisposable
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _applicationId = Guid.NewGuid();
    private readonly HeimdallDbContext _context;
    private readonly AccessDetailResolver _resolver;
    private readonly DateTime _evaluationTime = new(2024, 6, 15, 12, 0, 0, DateTimeKind.Utc);

    public AccessDetailResolverTests()
    {
        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(_tenantId);
        tenantContext.ActorEmail.Returns("test@test.com");

        var options = new DbContextOptionsBuilder<HeimdallDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new HeimdallDbContext(options, tenantContext);
        _resolver = new AccessDetailResolver(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    #region Helpers

    private UserProfile CreateActiveUser()
    {
        var user = new UserProfile
        {
            TenantId = _tenantId,
            ExternalSubjectId = $"ext-{Guid.NewGuid()}",
            IdentityProvider = "TestProvider",
            Email = $"user-{Guid.NewGuid()}@test.com",
            DisplayName = "Test User",
            Status = UserStatus.Active
        };
        _context.UserProfiles.Add(user);
        return user;
    }

    private FunctionalArea CreateActiveFunctionalArea(string? code = null)
    {
        var fa = new FunctionalArea
        {
            TenantId = _tenantId,
            ApplicationId = _applicationId,
            FunctionalAreaCode = code ?? $"FA_{Guid.NewGuid():N}"[..20].ToUpper(),
            Name = "Test Functional Area",
            IsActive = true
        };
        _context.FunctionalAreas.Add(fa);
        return fa;
    }

    private Permission CreateActivePermission(Guid functionalAreaId)
    {
        var permissionType = new PermissionType
        {
            TenantId = _tenantId,
            ApplicationId = _applicationId,
            Code = $"PT_{Guid.NewGuid():N}"[..20].ToUpper(),
            Name = "Test Permission Type",
            IsActive = true
        };
        _context.PermissionTypes.Add(permissionType);

        var permission = new Permission
        {
            TenantId = _tenantId,
            ApplicationId = _applicationId,
            FunctionalAreaId = functionalAreaId,
            PermissionTypeId = permissionType.Id,
            PermissionCode = $"PERM_{Guid.NewGuid():N}"[..30].ToUpper(),
            Name = "Test Permission",
            IsActive = true
        };
        _context.Permissions.Add(permission);
        return permission;
    }

    private void GrantUserPermission(Guid userProfileId, Guid permissionId)
    {
        _context.UserPermissionAssignments.Add(new UserPermissionAssignment
        {
            TenantId = _tenantId,
            ApplicationId = _applicationId,
            UserProfileId = userProfileId,
            PermissionId = permissionId,
            Effect = Effect.Allow,
            ValidFrom = _evaluationTime.AddDays(-30),
            ValidTo = _evaluationTime.AddDays(30)
        });
    }

    private Group CreateActiveGroup(string? name = null)
    {
        var group = new Group
        {
            TenantId = _tenantId,
            ApplicationId = _applicationId,
            Name = name ?? $"Group-{Guid.NewGuid()}",
            IsActive = true
        };
        _context.Groups.Add(group);
        return group;
    }

    private void AddUserToGroup(Guid userProfileId, Guid groupId)
    {
        _context.GroupMemberships.Add(new GroupMembership
        {
            TenantId = _tenantId,
            ApplicationId = _applicationId,
            GroupId = groupId,
            UserProfileId = userProfileId
        });
    }

    private void GrantGroupPermission(Guid groupId, Guid permissionId)
    {
        _context.GroupPermissionAssignments.Add(new GroupPermissionAssignment
        {
            TenantId = _tenantId,
            ApplicationId = _applicationId,
            GroupId = groupId,
            PermissionId = permissionId,
            Effect = Effect.Allow,
            ValidFrom = _evaluationTime.AddDays(-30),
            ValidTo = _evaluationTime.AddDays(30)
        });
    }

    private void AddFARequirement(Guid functionalAreaId, string accessDetailType, bool isActive = true)
    {
        _context.FunctionalAreaAccessRequirements.Add(new FunctionalAreaAccessRequirement
        {
            TenantId = _tenantId,
            ApplicationId = _applicationId,
            FunctionalAreaId = functionalAreaId,
            AccessDetailType = accessDetailType,
            IsRequired = true,
            IsActive = isActive
        });
    }

    private UserAccessDetail AddUserAccessDetail(
        Guid userProfileId,
        string accessDetailType,
        string accessDetailCode,
        string accessDetailValue = "TestValue",
        bool isActive = true,
        DateTime? validFrom = null,
        DateTime? validTo = null)
    {
        var detail = new UserAccessDetail
        {
            TenantId = _tenantId,
            ApplicationId = _applicationId,
            UserProfileId = userProfileId,
            AccessDetailType = accessDetailType,
            AccessDetailCode = accessDetailCode,
            AccessDetailValue = accessDetailValue,
            IsActive = isActive,
            ValidFrom = validFrom,
            ValidTo = validTo
        };
        _context.UserAccessDetails.Add(detail);
        return detail;
    }

    private GroupAccessDetail AddGroupAccessDetail(
        Guid groupId,
        string accessDetailType,
        string accessDetailCode,
        string accessDetailValue = "GroupValue",
        bool isActive = true,
        DateTime? validFrom = null,
        DateTime? validTo = null)
    {
        var detail = new GroupAccessDetail
        {
            TenantId = _tenantId,
            ApplicationId = _applicationId,
            GroupId = groupId,
            AccessDetailType = accessDetailType,
            AccessDetailCode = accessDetailCode,
            AccessDetailValue = accessDetailValue,
            IsActive = isActive,
            ValidFrom = validFrom,
            ValidTo = validTo
        };
        _context.GroupAccessDetails.Add(detail);
        return detail;
    }

    #endregion

    #region Scenario 1: User access details filtered by FA Access Requirement declarations

    [Fact]
    public async Task GetAccessDetails_OnlyReturnsDetails_MatchingActiveFARequirements()
    {
        // Arrange
        var user = CreateActiveUser();
        var fa = CreateActiveFunctionalArea("ORDERS");
        var permission = CreateActivePermission(fa.Id);
        GrantUserPermission(user.Id, permission.Id);

        // Declare "Region" as a required access detail type for this FA
        AddFARequirement(fa.Id, "Region");

        // Add user access details: one matching the requirement, one not
        var matchingDetail = AddUserAccessDetail(user.Id, "Region", "US_EAST", "us-east-1");
        AddUserAccessDetail(user.Id, "Department", "ENGINEERING", "eng-team");

        await _context.SaveChangesAsync();

        // Act
        var result = await _resolver.GetAccessDetailsAsync(
            _tenantId, _applicationId, user.Id, "ORDERS", _evaluationTime);

        // Assert
        Assert.Single(result.Details);
        Assert.Equal(matchingDetail.Id, result.Details[0].AccessDetailId);
        Assert.Equal("Region", result.Details[0].AccessDetailType);
        Assert.Equal("US_EAST", result.Details[0].AccessDetailCode);
        Assert.Equal("us-east-1", result.Details[0].AccessDetailValue);
    }

    [Fact]
    public async Task GetAccessDetails_MultipleFARequirements_ReturnsAllMatchingTypes()
    {
        // Arrange
        var user = CreateActiveUser();
        var fa = CreateActiveFunctionalArea("FINANCE");
        var permission = CreateActivePermission(fa.Id);
        GrantUserPermission(user.Id, permission.Id);

        // Declare two access detail types as requirements
        AddFARequirement(fa.Id, "CostCenter");
        AddFARequirement(fa.Id, "Region");

        // Add details for both required types plus an irrelevant one
        var costCenterDetail = AddUserAccessDetail(user.Id, "CostCenter", "CC100", "Engineering");
        var regionDetail = AddUserAccessDetail(user.Id, "Region", "EMEA", "Europe");
        AddUserAccessDetail(user.Id, "Project", "PROJ_X", "Secret Project");

        await _context.SaveChangesAsync();

        // Act
        var result = await _resolver.GetAccessDetailsAsync(
            _tenantId, _applicationId, user.Id, "FINANCE", _evaluationTime);

        // Assert
        Assert.Equal(2, result.Details.Count);
        var returnedIds = result.Details.Select(d => d.AccessDetailId).ToHashSet();
        Assert.Contains(costCenterDetail.Id, returnedIds);
        Assert.Contains(regionDetail.Id, returnedIds);
    }

    [Fact]
    public async Task GetAccessDetails_NoFARequirements_ReturnsEmptyDetails()
    {
        // Arrange
        var user = CreateActiveUser();
        var fa = CreateActiveFunctionalArea("REPORTS");
        var permission = CreateActivePermission(fa.Id);
        GrantUserPermission(user.Id, permission.Id);

        // No FA requirements declared
        AddUserAccessDetail(user.Id, "Region", "US_WEST", "us-west-2");

        await _context.SaveChangesAsync();

        // Act
        var result = await _resolver.GetAccessDetailsAsync(
            _tenantId, _applicationId, user.Id, "REPORTS", _evaluationTime);

        // Assert: no requirements means empty details (access based solely on permission)
        Assert.Empty(result.Details);
    }

    #endregion

    #region Scenario 2: Group access detail inheritance combines with direct user details

    [Fact]
    public async Task GetAccessDetails_CombinesDirectAndGroupAccessDetails()
    {
        // Arrange
        var user = CreateActiveUser();
        var fa = CreateActiveFunctionalArea("DASHBOARD");
        var permission = CreateActivePermission(fa.Id);
        GrantUserPermission(user.Id, permission.Id);

        AddFARequirement(fa.Id, "Region");

        // Direct user access detail
        var directDetail = AddUserAccessDetail(user.Id, "Region", "US_EAST", "us-east-1");

        // Group access detail inherited via membership
        var group = CreateActiveGroup("RegionalGroup");
        AddUserToGroup(user.Id, group.Id);
        var groupDetail = AddGroupAccessDetail(group.Id, "Region", "EU_WEST", "eu-west-1");

        await _context.SaveChangesAsync();

        // Act
        var result = await _resolver.GetAccessDetailsAsync(
            _tenantId, _applicationId, user.Id, "DASHBOARD", _evaluationTime);

        // Assert: both direct and group details are returned
        Assert.Equal(2, result.Details.Count);

        var directEntry = result.Details.First(d => d.AccessDetailId == directDetail.Id);
        Assert.Equal("DirectUser", directEntry.Source);
        Assert.Null(directEntry.GroupId);
        Assert.Null(directEntry.GroupName);

        var groupEntry = result.Details.First(d => d.AccessDetailId == groupDetail.Id);
        Assert.Equal("Group", groupEntry.Source);
        Assert.Equal(group.Id, groupEntry.GroupId);
        Assert.Equal("RegionalGroup", groupEntry.GroupName);
    }

    [Fact]
    public async Task GetAccessDetails_MultipleGroups_InheritsFromAllActiveGroups()
    {
        // Arrange
        var user = CreateActiveUser();
        var fa = CreateActiveFunctionalArea("ANALYTICS");
        var permission = CreateActivePermission(fa.Id);
        GrantUserPermission(user.Id, permission.Id);

        AddFARequirement(fa.Id, "Region");

        // User is a member of two active groups
        var group1 = CreateActiveGroup("GroupAlpha");
        var group2 = CreateActiveGroup("GroupBeta");
        AddUserToGroup(user.Id, group1.Id);
        AddUserToGroup(user.Id, group2.Id);

        var groupDetail1 = AddGroupAccessDetail(group1.Id, "Region", "US_EAST", "us-east-1");
        var groupDetail2 = AddGroupAccessDetail(group2.Id, "Region", "APAC", "ap-southeast-1");

        await _context.SaveChangesAsync();

        // Act
        var result = await _resolver.GetAccessDetailsAsync(
            _tenantId, _applicationId, user.Id, "ANALYTICS", _evaluationTime);

        // Assert: both group details are returned with correct source indicators
        Assert.Equal(2, result.Details.Count);

        var entry1 = result.Details.First(d => d.AccessDetailId == groupDetail1.Id);
        Assert.Equal("Group", entry1.Source);
        Assert.Equal(group1.Id, entry1.GroupId);
        Assert.Equal("GroupAlpha", entry1.GroupName);

        var entry2 = result.Details.First(d => d.AccessDetailId == groupDetail2.Id);
        Assert.Equal("Group", entry2.Source);
        Assert.Equal(group2.Id, entry2.GroupId);
        Assert.Equal("GroupBeta", entry2.GroupName);
    }

    [Fact]
    public async Task GetAccessDetails_InactiveGroup_ExcludesItsAccessDetails()
    {
        // Arrange
        var user = CreateActiveUser();
        var fa = CreateActiveFunctionalArea("SETTINGS");
        var permission = CreateActivePermission(fa.Id);
        GrantUserPermission(user.Id, permission.Id);

        AddFARequirement(fa.Id, "Region");

        // Active group with access detail
        var activeGroup = CreateActiveGroup("ActiveGroup");
        AddUserToGroup(user.Id, activeGroup.Id);
        var activeGroupDetail = AddGroupAccessDetail(activeGroup.Id, "Region", "US_WEST", "us-west-2");

        // Inactive group with access detail (should be excluded)
        var inactiveGroup = new Group
        {
            TenantId = _tenantId,
            ApplicationId = _applicationId,
            Name = "InactiveGroup",
            IsActive = false
        };
        _context.Groups.Add(inactiveGroup);
        _context.GroupMemberships.Add(new GroupMembership
        {
            TenantId = _tenantId,
            ApplicationId = _applicationId,
            GroupId = inactiveGroup.Id,
            UserProfileId = user.Id
        });
        AddGroupAccessDetail(inactiveGroup.Id, "Region", "INACTIVE_REGION", "inactive-region");

        await _context.SaveChangesAsync();

        // Act
        var result = await _resolver.GetAccessDetailsAsync(
            _tenantId, _applicationId, user.Id, "SETTINGS", _evaluationTime);

        // Assert: only active group's details are returned
        Assert.Single(result.Details);
        Assert.Equal(activeGroupDetail.Id, result.Details[0].AccessDetailId);
        Assert.Equal("Group", result.Details[0].Source);
    }

    [Fact]
    public async Task GetAccessDetails_GroupPermissionGivesAccess_CombinesWithDirectDetails()
    {
        // Arrange: user has permission via group (not direct), still gets access details
        var user = CreateActiveUser();
        var fa = CreateActiveFunctionalArea("INVENTORY");
        var permission = CreateActivePermission(fa.Id);

        var group = CreateActiveGroup("InventoryTeam");
        AddUserToGroup(user.Id, group.Id);
        GrantGroupPermission(group.Id, permission.Id);

        AddFARequirement(fa.Id, "Warehouse");

        var directDetail = AddUserAccessDetail(user.Id, "Warehouse", "WH_001", "Chicago");
        var groupDetail = AddGroupAccessDetail(group.Id, "Warehouse", "WH_002", "Denver");

        await _context.SaveChangesAsync();

        // Act
        var result = await _resolver.GetAccessDetailsAsync(
            _tenantId, _applicationId, user.Id, "INVENTORY", _evaluationTime);

        // Assert: both direct and group details returned even when permission is via group
        Assert.Equal(2, result.Details.Count);
        var returnedIds = result.Details.Select(d => d.AccessDetailId).ToHashSet();
        Assert.Contains(directDetail.Id, returnedIds);
        Assert.Contains(groupDetail.Id, returnedIds);
    }

    #endregion

    #region Scenario 3: Expired access detail (outside ValidFrom/ValidTo) excluded

    [Fact]
    public async Task GetAccessDetails_ExpiredUserAccessDetail_IsExcluded()
    {
        // Arrange
        var user = CreateActiveUser();
        var fa = CreateActiveFunctionalArea("BILLING");
        var permission = CreateActivePermission(fa.Id);
        GrantUserPermission(user.Id, permission.Id);

        AddFARequirement(fa.Id, "Account");

        // Valid detail (within time window)
        var validDetail = AddUserAccessDetail(user.Id, "Account", "ACC_001", "Premium",
            validFrom: _evaluationTime.AddDays(-10),
            validTo: _evaluationTime.AddDays(10));

        // Expired detail (ValidTo in the past)
        AddUserAccessDetail(user.Id, "Account", "ACC_002", "Legacy",
            validFrom: _evaluationTime.AddDays(-60),
            validTo: _evaluationTime.AddDays(-1));

        await _context.SaveChangesAsync();

        // Act
        var result = await _resolver.GetAccessDetailsAsync(
            _tenantId, _applicationId, user.Id, "BILLING", _evaluationTime);

        // Assert: only the valid detail is returned
        Assert.Single(result.Details);
        Assert.Equal(validDetail.Id, result.Details[0].AccessDetailId);
    }

    [Fact]
    public async Task GetAccessDetails_FutureUserAccessDetail_IsExcluded()
    {
        // Arrange
        var user = CreateActiveUser();
        var fa = CreateActiveFunctionalArea("SCHEDULING");
        var permission = CreateActivePermission(fa.Id);
        GrantUserPermission(user.Id, permission.Id);

        AddFARequirement(fa.Id, "Shift");

        // Future detail (ValidFrom in the future)
        AddUserAccessDetail(user.Id, "Shift", "SHIFT_FUTURE", "Night",
            validFrom: _evaluationTime.AddDays(5),
            validTo: _evaluationTime.AddDays(30));

        // Current detail
        var currentDetail = AddUserAccessDetail(user.Id, "Shift", "SHIFT_CURRENT", "Day",
            validFrom: _evaluationTime.AddDays(-5),
            validTo: _evaluationTime.AddDays(20));

        await _context.SaveChangesAsync();

        // Act
        var result = await _resolver.GetAccessDetailsAsync(
            _tenantId, _applicationId, user.Id, "SCHEDULING", _evaluationTime);

        // Assert: only the currently valid detail is returned
        Assert.Single(result.Details);
        Assert.Equal(currentDetail.Id, result.Details[0].AccessDetailId);
    }

    [Fact]
    public async Task GetAccessDetails_ExpiredGroupAccessDetail_IsExcluded()
    {
        // Arrange
        var user = CreateActiveUser();
        var fa = CreateActiveFunctionalArea("COMPLIANCE");
        var permission = CreateActivePermission(fa.Id);
        GrantUserPermission(user.Id, permission.Id);

        AddFARequirement(fa.Id, "Jurisdiction");

        var group = CreateActiveGroup("ComplianceTeam");
        AddUserToGroup(user.Id, group.Id);

        // Valid group detail
        var validGroupDetail = AddGroupAccessDetail(group.Id, "Jurisdiction", "US", "United States",
            validFrom: _evaluationTime.AddDays(-30),
            validTo: _evaluationTime.AddDays(30));

        // Expired group detail
        AddGroupAccessDetail(group.Id, "Jurisdiction", "EU_OLD", "EU (Expired)",
            validFrom: _evaluationTime.AddDays(-90),
            validTo: _evaluationTime.AddDays(-1));

        await _context.SaveChangesAsync();

        // Act
        var result = await _resolver.GetAccessDetailsAsync(
            _tenantId, _applicationId, user.Id, "COMPLIANCE", _evaluationTime);

        // Assert: only the non-expired group detail is returned
        Assert.Single(result.Details);
        Assert.Equal(validGroupDetail.Id, result.Details[0].AccessDetailId);
    }

    [Fact]
    public async Task GetAccessDetails_NullValidFromAndValidTo_TreatedAsAlwaysValid()
    {
        // Arrange
        var user = CreateActiveUser();
        var fa = CreateActiveFunctionalArea("ADMIN_PANEL");
        var permission = CreateActivePermission(fa.Id);
        GrantUserPermission(user.Id, permission.Id);

        AddFARequirement(fa.Id, "Role");

        // Detail with null ValidFrom and ValidTo (always valid)
        var alwaysValidDetail = AddUserAccessDetail(user.Id, "Role", "ADMIN", "Administrator",
            validFrom: null,
            validTo: null);

        await _context.SaveChangesAsync();

        // Act
        var result = await _resolver.GetAccessDetailsAsync(
            _tenantId, _applicationId, user.Id, "ADMIN_PANEL", _evaluationTime);

        // Assert: detail with null bounds is returned
        Assert.Single(result.Details);
        Assert.Equal(alwaysValidDetail.Id, result.Details[0].AccessDetailId);
    }

    #endregion

    #region Scenario 4: Inactive access detail excluded from results

    [Fact]
    public async Task GetAccessDetails_InactiveUserAccessDetail_IsExcluded()
    {
        // Arrange
        var user = CreateActiveUser();
        var fa = CreateActiveFunctionalArea("PRODUCTS");
        var permission = CreateActivePermission(fa.Id);
        GrantUserPermission(user.Id, permission.Id);

        AddFARequirement(fa.Id, "Category");

        // Active detail
        var activeDetail = AddUserAccessDetail(user.Id, "Category", "ELECTRONICS", "Electronics",
            isActive: true,
            validFrom: _evaluationTime.AddDays(-10),
            validTo: _evaluationTime.AddDays(10));

        // Inactive detail (should be excluded)
        AddUserAccessDetail(user.Id, "Category", "FURNITURE", "Furniture",
            isActive: false,
            validFrom: _evaluationTime.AddDays(-10),
            validTo: _evaluationTime.AddDays(10));

        await _context.SaveChangesAsync();

        // Act
        var result = await _resolver.GetAccessDetailsAsync(
            _tenantId, _applicationId, user.Id, "PRODUCTS", _evaluationTime);

        // Assert: only active detail is returned
        Assert.Single(result.Details);
        Assert.Equal(activeDetail.Id, result.Details[0].AccessDetailId);
    }

    [Fact]
    public async Task GetAccessDetails_InactiveGroupAccessDetail_IsExcluded()
    {
        // Arrange
        var user = CreateActiveUser();
        var fa = CreateActiveFunctionalArea("NOTIFICATIONS");
        var permission = CreateActivePermission(fa.Id);
        GrantUserPermission(user.Id, permission.Id);

        AddFARequirement(fa.Id, "Channel");

        var group = CreateActiveGroup("NotifyGroup");
        AddUserToGroup(user.Id, group.Id);

        // Active group detail
        var activeGroupDetail = AddGroupAccessDetail(group.Id, "Channel", "EMAIL", "Email channel",
            isActive: true,
            validFrom: _evaluationTime.AddDays(-10),
            validTo: _evaluationTime.AddDays(10));

        // Inactive group detail
        AddGroupAccessDetail(group.Id, "Channel", "SMS", "SMS channel",
            isActive: false,
            validFrom: _evaluationTime.AddDays(-10),
            validTo: _evaluationTime.AddDays(10));

        await _context.SaveChangesAsync();

        // Act
        var result = await _resolver.GetAccessDetailsAsync(
            _tenantId, _applicationId, user.Id, "NOTIFICATIONS", _evaluationTime);

        // Assert: only active group detail is returned
        Assert.Single(result.Details);
        Assert.Equal(activeGroupDetail.Id, result.Details[0].AccessDetailId);
    }

    [Fact]
    public async Task GetAccessDetails_AllInactiveDetails_ReturnsEmpty()
    {
        // Arrange
        var user = CreateActiveUser();
        var fa = CreateActiveFunctionalArea("ARCHIVE");
        var permission = CreateActivePermission(fa.Id);
        GrantUserPermission(user.Id, permission.Id);

        AddFARequirement(fa.Id, "Section");

        // All details are inactive
        AddUserAccessDetail(user.Id, "Section", "SEC_A", "Section A",
            isActive: false,
            validFrom: _evaluationTime.AddDays(-10),
            validTo: _evaluationTime.AddDays(10));

        AddUserAccessDetail(user.Id, "Section", "SEC_B", "Section B",
            isActive: false,
            validFrom: _evaluationTime.AddDays(-10),
            validTo: _evaluationTime.AddDays(10));

        await _context.SaveChangesAsync();

        // Act
        var result = await _resolver.GetAccessDetailsAsync(
            _tenantId, _applicationId, user.Id, "ARCHIVE", _evaluationTime);

        // Assert: no details returned since all are inactive
        Assert.Empty(result.Details);
    }

    #endregion
}
