using Heimdall.Domain.Entities;
using Heimdall.Domain.Enums;
using Heimdall.Domain.Interfaces;
using Heimdall.Domain.Models;
using Heimdall.Infrastructure.Caching;
using Heimdall.Infrastructure.Logging;
using Heimdall.Infrastructure.Persistence;
using Heimdall.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Heimdall.Infrastructure.Tests.Unit;

/// <summary>
/// Unit tests for TemplateApplicationService covering template materialization scenarios.
/// Validates: Requirements 27.2
/// </summary>
public class TemplateApplicationServiceTests : IDisposable
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _applicationId = Guid.NewGuid();
    private readonly ITenantContext _tenantContext;
    private readonly HeimdallDbContext _db;
    private readonly ICacheService _cacheService;
    private readonly TemplateApplicationService _service;

    public TemplateApplicationServiceTests()
    {
        _tenantContext = Substitute.For<ITenantContext>();
        _tenantContext.TenantId.Returns(_tenantId);
        _tenantContext.ActorEmail.Returns("admin@test.com");

        var options = new DbContextOptionsBuilder<HeimdallDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _db = new HeimdallDbContext(options, _tenantContext);

        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cache:DefaultTtlSeconds"] = "300"
            })
            .Build();

        _cacheService = new InMemoryCacheService(memoryCache, config, NullLogger<InMemoryCacheService>.Instance, NullHeimdallMetricsService.Instance);
        _service = new TemplateApplicationService(_db, _cacheService, _tenantContext);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    #region Helpers

    private UserProfile CreateUser()
    {
        var user = new UserProfile
        {
            TenantId = _tenantId,
            ExternalSubjectId = $"ext-{Guid.NewGuid()}",
            IdentityProvider = "TestIdP",
            Email = $"user-{Guid.NewGuid()}@test.com",
            DisplayName = "Test User",
            Status = UserStatus.Active
        };
        _db.UserProfiles.Add(user);
        return user;
    }

    private PermissionTemplate CreateActiveTemplate()
    {
        var template = new PermissionTemplate
        {
            TenantId = _tenantId,
            ApplicationId = _applicationId,
            TemplateCode = $"TPL_{Guid.NewGuid():N}"[..20].ToUpper(),
            Name = "Test Template",
            IsActive = true
        };
        _db.PermissionTemplates.Add(template);
        return template;
    }

    private Permission CreatePermission()
    {
        var fa = new FunctionalArea
        {
            TenantId = _tenantId,
            ApplicationId = _applicationId,
            FunctionalAreaCode = $"FA_{Guid.NewGuid():N}"[..20].ToUpper(),
            Name = "Test FA",
            IsActive = true
        };
        _db.FunctionalAreas.Add(fa);

        var pt = new PermissionType
        {
            TenantId = _tenantId,
            ApplicationId = _applicationId,
            Code = $"PT_{Guid.NewGuid():N}"[..20].ToUpper(),
            Name = "Test PT",
            IsActive = true
        };
        _db.PermissionTypes.Add(pt);

        var permission = new Permission
        {
            TenantId = _tenantId,
            ApplicationId = _applicationId,
            FunctionalAreaId = fa.Id,
            PermissionTypeId = pt.Id,
            PermissionCode = $"PERM_{Guid.NewGuid():N}"[..30].ToUpper(),
            Name = "Test Permission",
            IsActive = true
        };
        _db.Permissions.Add(permission);
        return permission;
    }

    private Group CreateGroup(string? name = null)
    {
        var group = new Group
        {
            TenantId = _tenantId,
            ApplicationId = _applicationId,
            Name = name ?? $"Group-{Guid.NewGuid()}",
            IsActive = true
        };
        _db.Groups.Add(group);
        return group;
    }

    private static TemplateApplicationOptions DefaultOptions(
        bool replacePermissions = false,
        bool replaceGroups = false,
        bool replaceAccessDetails = false) => new()
    {
        ReplaceExistingPermissions = replacePermissions,
        ReplaceExistingGroups = replaceGroups,
        ReplaceExistingAccessDetails = replaceAccessDetails
    };

    #endregion

    #region Template applies direct permission assignments to target user

    [Fact]
    public async Task ApplyTemplate_WithPermissions_CreatesUserPermissionAssignments()
    {
        // Arrange
        var user = CreateUser();
        var template = CreateActiveTemplate();
        var perm1 = CreatePermission();
        var perm2 = CreatePermission();

        _db.PermissionTemplatePermissions.AddRange(
            new PermissionTemplatePermission
            {
                PermissionTemplateId = template.Id,
                PermissionId = perm1.Id,
                Effect = Effect.Allow
            },
            new PermissionTemplatePermission
            {
                PermissionTemplateId = template.Id,
                PermissionId = perm2.Id,
                Effect = Effect.Deny
            });

        await _db.SaveChangesAsync();

        // Act
        var result = await _service.ApplyTemplateAsync(
            _tenantId, _applicationId, user.Id, template.Id, DefaultOptions());

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, result.PermissionsApplied);

        var assignments = await _db.UserPermissionAssignments
            .IgnoreQueryFilters()
            .Where(a => a.UserProfileId == user.Id)
            .ToListAsync();

        Assert.Equal(2, assignments.Count);
        Assert.Contains(assignments, a => a.PermissionId == perm1.Id && a.Effect == Effect.Allow);
        Assert.Contains(assignments, a => a.PermissionId == perm2.Id && a.Effect == Effect.Deny);
    }

    [Fact]
    public async Task ApplyTemplate_WithPermissionOffsetDays_CalculatesValidFromAndValidTo()
    {
        // Arrange
        var user = CreateUser();
        var template = CreateActiveTemplate();
        var perm = CreatePermission();

        _db.PermissionTemplatePermissions.Add(new PermissionTemplatePermission
        {
            PermissionTemplateId = template.Id,
            PermissionId = perm.Id,
            Effect = Effect.Allow,
            ValidFromOffsetDays = 5,
            ValidToOffsetDays = 30
        });

        await _db.SaveChangesAsync();

        var beforeApply = DateTime.UtcNow;

        // Act
        var result = await _service.ApplyTemplateAsync(
            _tenantId, _applicationId, user.Id, template.Id, DefaultOptions());

        var afterApply = DateTime.UtcNow;

        // Assert
        Assert.True(result.Success);

        var assignment = await _db.UserPermissionAssignments
            .IgnoreQueryFilters()
            .SingleAsync(a => a.UserProfileId == user.Id);

        Assert.NotNull(assignment.ValidFrom);
        Assert.NotNull(assignment.ValidTo);

        // ValidFrom should be approximately now + 5 days
        Assert.True(assignment.ValidFrom >= beforeApply.AddDays(5));
        Assert.True(assignment.ValidFrom <= afterApply.AddDays(5));

        // ValidTo should be approximately now + 30 days
        Assert.True(assignment.ValidTo >= beforeApply.AddDays(30));
        Assert.True(assignment.ValidTo <= afterApply.AddDays(30));
    }

    #endregion

    #region Template applies group memberships to target user

    [Fact]
    public async Task ApplyTemplate_WithGroups_CreatesGroupMemberships()
    {
        // Arrange
        var user = CreateUser();
        var template = CreateActiveTemplate();
        var group1 = CreateGroup("Editors");
        var group2 = CreateGroup("Viewers");

        _db.PermissionTemplateGroups.AddRange(
            new PermissionTemplateGroup
            {
                PermissionTemplateId = template.Id,
                GroupId = group1.Id
            },
            new PermissionTemplateGroup
            {
                PermissionTemplateId = template.Id,
                GroupId = group2.Id
            });

        await _db.SaveChangesAsync();

        // Act
        var result = await _service.ApplyTemplateAsync(
            _tenantId, _applicationId, user.Id, template.Id, DefaultOptions());

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, result.GroupMembershipsApplied);

        var memberships = await _db.GroupMemberships
            .IgnoreQueryFilters()
            .Where(gm => gm.UserProfileId == user.Id)
            .ToListAsync();

        Assert.Equal(2, memberships.Count);
        Assert.Contains(memberships, gm => gm.GroupId == group1.Id);
        Assert.Contains(memberships, gm => gm.GroupId == group2.Id);
    }

    [Fact]
    public async Task ApplyTemplate_WithGroupAlreadyMember_SkipsDuplicate()
    {
        // Arrange
        var user = CreateUser();
        var template = CreateActiveTemplate();
        var group = CreateGroup("ExistingGroup");

        // User already a member
        _db.GroupMemberships.Add(new GroupMembership
        {
            TenantId = _tenantId,
            ApplicationId = _applicationId,
            UserProfileId = user.Id,
            GroupId = group.Id
        });

        _db.PermissionTemplateGroups.Add(new PermissionTemplateGroup
        {
            PermissionTemplateId = template.Id,
            GroupId = group.Id
        });

        await _db.SaveChangesAsync();

        // Act — without replace, should skip the existing membership
        var result = await _service.ApplyTemplateAsync(
            _tenantId, _applicationId, user.Id, template.Id, DefaultOptions());

        // Assert
        Assert.True(result.Success);
        Assert.Equal(0, result.GroupMembershipsApplied);

        var memberships = await _db.GroupMemberships
            .IgnoreQueryFilters()
            .Where(gm => gm.UserProfileId == user.Id)
            .ToListAsync();

        Assert.Single(memberships);
    }

    #endregion

    #region Template applies access detail records with offset day calculation

    [Fact]
    public async Task ApplyTemplate_WithAccessDetails_CreatesUserAccessDetails()
    {
        // Arrange
        var user = CreateUser();
        var template = CreateActiveTemplate();

        _db.PermissionTemplateAccessDetails.AddRange(
            new PermissionTemplateAccessDetail
            {
                PermissionTemplateId = template.Id,
                AccessDetailType = "Region",
                AccessDetailCode = "US_WEST",
                AccessDetailValue = "West Region",
                Description = "Access to west region"
            },
            new PermissionTemplateAccessDetail
            {
                PermissionTemplateId = template.Id,
                AccessDetailType = "Department",
                AccessDetailCode = "ENG",
                AccessDetailValue = "Engineering",
                Description = "Engineering department"
            });

        await _db.SaveChangesAsync();

        // Act
        var result = await _service.ApplyTemplateAsync(
            _tenantId, _applicationId, user.Id, template.Id, DefaultOptions());

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, result.AccessDetailsApplied);

        var accessDetails = await _db.UserAccessDetails
            .IgnoreQueryFilters()
            .Where(ad => ad.UserProfileId == user.Id)
            .ToListAsync();

        Assert.Equal(2, accessDetails.Count);
        Assert.Contains(accessDetails, ad =>
            ad.AccessDetailType == "Region" &&
            ad.AccessDetailCode == "US_WEST" &&
            ad.AccessDetailValue == "West Region");
        Assert.Contains(accessDetails, ad =>
            ad.AccessDetailType == "Department" &&
            ad.AccessDetailCode == "ENG" &&
            ad.AccessDetailValue == "Engineering");
    }

    [Fact]
    public async Task ApplyTemplate_WithAccessDetailOffsets_CalculatesValidFromAndValidTo()
    {
        // Arrange
        var user = CreateUser();
        var template = CreateActiveTemplate();

        _db.PermissionTemplateAccessDetails.Add(new PermissionTemplateAccessDetail
        {
            PermissionTemplateId = template.Id,
            AccessDetailType = "Contract",
            AccessDetailCode = "VENDOR_A",
            AccessDetailValue = "Active",
            ValidFromOffsetDays = 0,
            ValidToOffsetDays = 90
        });

        await _db.SaveChangesAsync();

        var beforeApply = DateTime.UtcNow;

        // Act
        var result = await _service.ApplyTemplateAsync(
            _tenantId, _applicationId, user.Id, template.Id, DefaultOptions());

        var afterApply = DateTime.UtcNow;

        // Assert
        Assert.True(result.Success);

        var ad = await _db.UserAccessDetails
            .IgnoreQueryFilters()
            .SingleAsync(a => a.UserProfileId == user.Id);

        Assert.NotNull(ad.ValidFrom);
        Assert.NotNull(ad.ValidTo);

        // ValidFrom with 0 offset = approximately now
        Assert.True(ad.ValidFrom >= beforeApply);
        Assert.True(ad.ValidFrom <= afterApply);

        // ValidTo with 90 offset = approximately now + 90 days
        Assert.True(ad.ValidTo >= beforeApply.AddDays(90));
        Assert.True(ad.ValidTo <= afterApply.AddDays(90));
    }

    [Fact]
    public async Task ApplyTemplate_WithNullOffsets_LeavesValidFromAndValidToNull()
    {
        // Arrange
        var user = CreateUser();
        var template = CreateActiveTemplate();

        _db.PermissionTemplateAccessDetails.Add(new PermissionTemplateAccessDetail
        {
            PermissionTemplateId = template.Id,
            AccessDetailType = "Region",
            AccessDetailCode = "GLOBAL",
            AccessDetailValue = "All Regions",
            ValidFromOffsetDays = null,
            ValidToOffsetDays = null
        });

        await _db.SaveChangesAsync();

        // Act
        var result = await _service.ApplyTemplateAsync(
            _tenantId, _applicationId, user.Id, template.Id, DefaultOptions());

        // Assert
        Assert.True(result.Success);

        var ad = await _db.UserAccessDetails
            .IgnoreQueryFilters()
            .SingleAsync(a => a.UserProfileId == user.Id);

        Assert.Null(ad.ValidFrom);
        Assert.Null(ad.ValidTo);
    }

    #endregion

    #region Template replace option removes existing before materializing

    [Fact]
    public async Task ApplyTemplate_ReplacePermissions_RemovesExistingBeforeMaterializing()
    {
        // Arrange
        var user = CreateUser();
        var template = CreateActiveTemplate();
        var existingPerm = CreatePermission();
        var newPerm = CreatePermission();

        // Existing assignment for the user
        _db.UserPermissionAssignments.Add(new UserPermissionAssignment
        {
            TenantId = _tenantId,
            ApplicationId = _applicationId,
            UserProfileId = user.Id,
            PermissionId = existingPerm.Id,
            Effect = Effect.Allow
        });

        // Template has a different permission
        _db.PermissionTemplatePermissions.Add(new PermissionTemplatePermission
        {
            PermissionTemplateId = template.Id,
            PermissionId = newPerm.Id,
            Effect = Effect.Allow
        });

        await _db.SaveChangesAsync();

        // Act
        var result = await _service.ApplyTemplateAsync(
            _tenantId, _applicationId, user.Id, template.Id,
            DefaultOptions(replacePermissions: true));

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, result.PermissionsApplied);

        var assignments = await _db.UserPermissionAssignments
            .IgnoreQueryFilters()
            .Where(a => a.UserProfileId == user.Id)
            .ToListAsync();

        // Only the new permission should remain
        Assert.Single(assignments);
        Assert.Equal(newPerm.Id, assignments[0].PermissionId);
    }

    [Fact]
    public async Task ApplyTemplate_ReplaceGroups_RemovesExistingBeforeMaterializing()
    {
        // Arrange
        var user = CreateUser();
        var template = CreateActiveTemplate();
        var existingGroup = CreateGroup("OldGroup");
        var newGroup = CreateGroup("NewGroup");

        // Existing membership
        _db.GroupMemberships.Add(new GroupMembership
        {
            TenantId = _tenantId,
            ApplicationId = _applicationId,
            UserProfileId = user.Id,
            GroupId = existingGroup.Id
        });

        // Template has a different group
        _db.PermissionTemplateGroups.Add(new PermissionTemplateGroup
        {
            PermissionTemplateId = template.Id,
            GroupId = newGroup.Id
        });

        await _db.SaveChangesAsync();

        // Act
        var result = await _service.ApplyTemplateAsync(
            _tenantId, _applicationId, user.Id, template.Id,
            DefaultOptions(replaceGroups: true));

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, result.GroupMembershipsApplied);

        var memberships = await _db.GroupMemberships
            .IgnoreQueryFilters()
            .Where(gm => gm.UserProfileId == user.Id)
            .ToListAsync();

        Assert.Single(memberships);
        Assert.Equal(newGroup.Id, memberships[0].GroupId);
    }

    [Fact]
    public async Task ApplyTemplate_ReplaceAccessDetails_RemovesExistingBeforeMaterializing()
    {
        // Arrange
        var user = CreateUser();
        var template = CreateActiveTemplate();

        // Existing access detail
        _db.UserAccessDetails.Add(new UserAccessDetail
        {
            TenantId = _tenantId,
            ApplicationId = _applicationId,
            UserProfileId = user.Id,
            AccessDetailType = "OldType",
            AccessDetailCode = "OLD_CODE",
            AccessDetailValue = "Old Value",
            IsActive = true
        });

        // Template has a new access detail
        _db.PermissionTemplateAccessDetails.Add(new PermissionTemplateAccessDetail
        {
            PermissionTemplateId = template.Id,
            AccessDetailType = "NewType",
            AccessDetailCode = "NEW_CODE",
            AccessDetailValue = "New Value"
        });

        await _db.SaveChangesAsync();

        // Act
        var result = await _service.ApplyTemplateAsync(
            _tenantId, _applicationId, user.Id, template.Id,
            DefaultOptions(replaceAccessDetails: true));

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, result.AccessDetailsApplied);

        var details = await _db.UserAccessDetails
            .IgnoreQueryFilters()
            .Where(ad => ad.UserProfileId == user.Id)
            .ToListAsync();

        Assert.Single(details);
        Assert.Equal("NewType", details[0].AccessDetailType);
        Assert.Equal("NEW_CODE", details[0].AccessDetailCode);
    }

    #endregion

    #region Template preview returns projected assignments without persisting

    [Fact]
    public async Task PreviewTemplate_ReturnsProjectedChanges_WithoutPersisting()
    {
        // Arrange
        var user = CreateUser();
        var template = CreateActiveTemplate();
        var perm = CreatePermission();
        var group = CreateGroup("PreviewGroup");

        _db.PermissionTemplatePermissions.Add(new PermissionTemplatePermission
        {
            PermissionTemplateId = template.Id,
            PermissionId = perm.Id,
            Effect = Effect.Allow
        });

        _db.PermissionTemplateGroups.Add(new PermissionTemplateGroup
        {
            PermissionTemplateId = template.Id,
            GroupId = group.Id
        });

        _db.PermissionTemplateAccessDetails.Add(new PermissionTemplateAccessDetail
        {
            PermissionTemplateId = template.Id,
            AccessDetailType = "Region",
            AccessDetailCode = "EMEA",
            AccessDetailValue = "Europe"
        });

        await _db.SaveChangesAsync();

        // Act
        var preview = await _service.PreviewTemplateAsync(
            _tenantId, _applicationId, user.Id, template.Id, DefaultOptions());

        // Assert — projected changes returned
        Assert.Single(preview.PermissionChanges);
        Assert.Equal("Add", preview.PermissionChanges[0].ChangeType);
        Assert.Equal(perm.Id, preview.PermissionChanges[0].PermissionId);

        Assert.Single(preview.GroupChanges);
        Assert.Equal("Add", preview.GroupChanges[0].ChangeType);
        Assert.Equal(group.Id, preview.GroupChanges[0].GroupId);

        Assert.Single(preview.AccessDetailChanges);
        Assert.Equal("Add", preview.AccessDetailChanges[0].ChangeType);
        Assert.Equal("Region", preview.AccessDetailChanges[0].AccessDetailType);

        // Assert — nothing persisted
        var assignments = await _db.UserPermissionAssignments
            .IgnoreQueryFilters()
            .Where(a => a.UserProfileId == user.Id)
            .ToListAsync();
        Assert.Empty(assignments);

        var memberships = await _db.GroupMemberships
            .IgnoreQueryFilters()
            .Where(gm => gm.UserProfileId == user.Id)
            .ToListAsync();
        Assert.Empty(memberships);

        var accessDetails = await _db.UserAccessDetails
            .IgnoreQueryFilters()
            .Where(ad => ad.UserProfileId == user.Id)
            .ToListAsync();
        Assert.Empty(accessDetails);
    }

    [Fact]
    public async Task PreviewTemplate_WithExistingAssignments_ShowsUnchanged()
    {
        // Arrange
        var user = CreateUser();
        var template = CreateActiveTemplate();
        var perm = CreatePermission();

        // User already has this permission
        _db.UserPermissionAssignments.Add(new UserPermissionAssignment
        {
            TenantId = _tenantId,
            ApplicationId = _applicationId,
            UserProfileId = user.Id,
            PermissionId = perm.Id,
            Effect = Effect.Allow
        });

        // Template also has this permission
        _db.PermissionTemplatePermissions.Add(new PermissionTemplatePermission
        {
            PermissionTemplateId = template.Id,
            PermissionId = perm.Id,
            Effect = Effect.Allow
        });

        await _db.SaveChangesAsync();

        // Act
        var preview = await _service.PreviewTemplateAsync(
            _tenantId, _applicationId, user.Id, template.Id, DefaultOptions());

        // Assert — shows as unchanged since it already exists
        Assert.Single(preview.PermissionChanges);
        Assert.Equal("Unchanged", preview.PermissionChanges[0].ChangeType);
    }

    [Fact]
    public async Task PreviewTemplate_WithReplaceOption_ShowsRemovals()
    {
        // Arrange
        var user = CreateUser();
        var template = CreateActiveTemplate();
        var existingPerm = CreatePermission();
        var newPerm = CreatePermission();

        // User has an existing permission NOT in the template
        _db.UserPermissionAssignments.Add(new UserPermissionAssignment
        {
            TenantId = _tenantId,
            ApplicationId = _applicationId,
            UserProfileId = user.Id,
            PermissionId = existingPerm.Id,
            Effect = Effect.Allow
        });

        // Template only has the new permission
        _db.PermissionTemplatePermissions.Add(new PermissionTemplatePermission
        {
            PermissionTemplateId = template.Id,
            PermissionId = newPerm.Id,
            Effect = Effect.Allow
        });

        await _db.SaveChangesAsync();

        // Act
        var preview = await _service.PreviewTemplateAsync(
            _tenantId, _applicationId, user.Id, template.Id,
            DefaultOptions(replacePermissions: true));

        // Assert — existing will be removed, new will be added
        Assert.Equal(2, preview.PermissionChanges.Count);
        Assert.Contains(preview.PermissionChanges, c =>
            c.PermissionId == existingPerm.Id && c.ChangeType == "Remove");
        Assert.Contains(preview.PermissionChanges, c =>
            c.PermissionId == newPerm.Id && c.ChangeType == "Add");
    }

    #endregion
}
