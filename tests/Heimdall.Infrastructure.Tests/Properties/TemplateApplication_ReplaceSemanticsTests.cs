using FsCheck;
using FsCheck.Xunit;
using Heimdall.Domain.Entities;
using Heimdall.Domain.Enums;
using Heimdall.Domain.Interfaces;
using Heimdall.Domain.Models;
using Heimdall.Infrastructure.Persistence;
using Heimdall.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;

namespace Heimdall.Infrastructure.Tests.Properties;

/// <summary>
/// Property 16: Template Replace Semantics
/// Generate existing assignments + template with replace flags; assert only template-derived records remain after apply.
///
/// **Validates: Requirements 15.4, 15.5, 15.6**
/// </summary>
public class TemplateApplication_ReplaceSemanticsTests
{
    private static HeimdallDbContext CreateDbContext(ITenantContext tenantContext)
    {
        var options = new DbContextOptionsBuilder<HeimdallDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new HeimdallDbContext(options, tenantContext);
    }

    [Property(MaxTest = 50)]
    public bool ReplacePermissions_RemovesOldAndKeepsOnlyTemplateDerived(
        PositiveInt existingCountRaw, PositiveInt templateCountRaw)
    {
        var existingCount = (existingCountRaw.Get % 5) + 1;
        var templateCount = (templateCountRaw.Get % 5) + 1;

        return RunReplacePermissionsTestAsync(existingCount, templateCount).GetAwaiter().GetResult();
    }

    private static async Task<bool> RunReplacePermissionsTestAsync(int existingCount, int templateCount)
    {
        var tenantId = Guid.NewGuid();
        var applicationId = Guid.NewGuid();

        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(tenantId);
        tenantContext.ActorEmail.Returns("test@test.com");

        var cacheService = Substitute.For<ICacheService>();

        using var context = CreateDbContext(tenantContext);

        // Create user
        var userProfile = new UserProfile
        {
            TenantId = tenantId,
            ExternalSubjectId = $"ext-{Guid.NewGuid()}",
            IdentityProvider = "TestProvider",
            Email = $"user-{Guid.NewGuid()}@test.com",
            DisplayName = "Test User",
            Status = UserStatus.Active
        };
        context.UserProfiles.Add(userProfile);

        // Create functional area and permission type (shared)
        var functionalArea = new FunctionalArea
        {
            TenantId = tenantId,
            ApplicationId = applicationId,
            FunctionalAreaCode = $"FA_{Guid.NewGuid():N}"[..20].ToUpper(),
            Name = "Test FA",
            IsActive = true
        };
        context.FunctionalAreas.Add(functionalArea);

        var permissionType = new PermissionType
        {
            TenantId = tenantId,
            ApplicationId = applicationId,
            Code = $"PT_{Guid.NewGuid():N}"[..20].ToUpper(),
            Name = "Test PT",
            IsActive = true
        };
        context.PermissionTypes.Add(permissionType);

        // Create existing permission assignments with their own permissions
        var existingPermissionIds = new List<Guid>();
        for (int i = 0; i < existingCount; i++)
        {
            var perm = new Permission
            {
                TenantId = tenantId,
                ApplicationId = applicationId,
                FunctionalAreaId = functionalArea.Id,
                PermissionTypeId = permissionType.Id,
                PermissionCode = $"EXISTING_{Guid.NewGuid():N}"[..30].ToUpper(),
                Name = $"Existing Permission {i}",
                IsActive = true
            };
            context.Permissions.Add(perm);
            existingPermissionIds.Add(perm.Id);

            context.UserPermissionAssignments.Add(new UserPermissionAssignment
            {
                TenantId = tenantId,
                ApplicationId = applicationId,
                UserProfileId = userProfile.Id,
                PermissionId = perm.Id,
                Effect = Effect.Allow,
                ValidFrom = null,
                ValidTo = null
            });
        }

        // Create template permissions (different permissions)
        var template = new PermissionTemplate
        {
            TenantId = tenantId,
            ApplicationId = applicationId,
            TemplateCode = $"TPL_{Guid.NewGuid():N}"[..20].ToUpper(),
            Name = "Test Template",
            IsActive = true
        };
        context.PermissionTemplates.Add(template);

        var templatePermissionIds = new List<Guid>();
        for (int i = 0; i < templateCount; i++)
        {
            var perm = new Permission
            {
                TenantId = tenantId,
                ApplicationId = applicationId,
                FunctionalAreaId = functionalArea.Id,
                PermissionTypeId = permissionType.Id,
                PermissionCode = $"TEMPLATE_{Guid.NewGuid():N}"[..30].ToUpper(),
                Name = $"Template Permission {i}",
                IsActive = true
            };
            context.Permissions.Add(perm);
            templatePermissionIds.Add(perm.Id);

            context.PermissionTemplatePermissions.Add(new PermissionTemplatePermission
            {
                PermissionTemplateId = template.Id,
                PermissionId = perm.Id,
                Effect = Effect.Allow,
                ValidFromOffsetDays = null,
                ValidToOffsetDays = null
            });
        }

        await context.SaveChangesAsync();

        // Apply template with ReplaceExistingPermissions = true
        var service = new TemplateApplicationService(context, cacheService, tenantContext);
        var options = new TemplateApplicationOptions
        {
            ReplaceExistingPermissions = true,
            ReplaceExistingGroups = false,
            ReplaceExistingAccessDetails = false
        };

        var result = await service.ApplyTemplateAsync(
            tenantId, applicationId, userProfile.Id, template.Id, options);

        if (!result.Success)
            return false;

        // Assert: old permission assignments are gone, only template-derived exist
        var remainingAssignments = await context.UserPermissionAssignments
            .IgnoreQueryFilters()
            .Where(a => a.TenantId == tenantId &&
                        a.ApplicationId == applicationId &&
                        a.UserProfileId == userProfile.Id)
            .ToListAsync();

        // None of the existing permissions should remain
        var hasOldPermissions = remainingAssignments.Any(a => existingPermissionIds.Contains(a.PermissionId));
        if (hasOldPermissions)
            return false;

        // All template permission IDs should be present
        var remainingPermIds = remainingAssignments.Select(a => a.PermissionId).ToHashSet();
        var allTemplatePresent = templatePermissionIds.All(id => remainingPermIds.Contains(id));
        if (!allTemplatePresent)
            return false;

        // Count should match template count exactly
        return remainingAssignments.Count == templateCount;
    }

    [Property(MaxTest = 50)]
    public bool ReplaceGroups_RemovesOldAndKeepsOnlyTemplateDerived(
        PositiveInt existingCountRaw, PositiveInt templateCountRaw)
    {
        var existingCount = (existingCountRaw.Get % 5) + 1;
        var templateCount = (templateCountRaw.Get % 5) + 1;

        return RunReplaceGroupsTestAsync(existingCount, templateCount).GetAwaiter().GetResult();
    }

    private static async Task<bool> RunReplaceGroupsTestAsync(int existingCount, int templateCount)
    {
        var tenantId = Guid.NewGuid();
        var applicationId = Guid.NewGuid();

        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(tenantId);
        tenantContext.ActorEmail.Returns("test@test.com");

        var cacheService = Substitute.For<ICacheService>();

        using var context = CreateDbContext(tenantContext);

        // Create user
        var userProfile = new UserProfile
        {
            TenantId = tenantId,
            ExternalSubjectId = $"ext-{Guid.NewGuid()}",
            IdentityProvider = "TestProvider",
            Email = $"user-{Guid.NewGuid()}@test.com",
            DisplayName = "Test User",
            Status = UserStatus.Active
        };
        context.UserProfiles.Add(userProfile);

        // Create existing group memberships
        var existingGroupIds = new List<Guid>();
        for (int i = 0; i < existingCount; i++)
        {
            var group = new Group
            {
                TenantId = tenantId,
                ApplicationId = applicationId,
                Name = $"ExistingGroup-{Guid.NewGuid()}",
                IsActive = true
            };
            context.Groups.Add(group);
            existingGroupIds.Add(group.Id);

            context.GroupMemberships.Add(new GroupMembership
            {
                TenantId = tenantId,
                ApplicationId = applicationId,
                GroupId = group.Id,
                UserProfileId = userProfile.Id
            });
        }

        // Create template with new group entries
        var template = new PermissionTemplate
        {
            TenantId = tenantId,
            ApplicationId = applicationId,
            TemplateCode = $"TPL_{Guid.NewGuid():N}"[..20].ToUpper(),
            Name = "Test Template",
            IsActive = true
        };
        context.PermissionTemplates.Add(template);

        var templateGroupIds = new List<Guid>();
        for (int i = 0; i < templateCount; i++)
        {
            var group = new Group
            {
                TenantId = tenantId,
                ApplicationId = applicationId,
                Name = $"TemplateGroup-{Guid.NewGuid()}",
                IsActive = true
            };
            context.Groups.Add(group);
            templateGroupIds.Add(group.Id);

            context.PermissionTemplateGroups.Add(new PermissionTemplateGroup
            {
                PermissionTemplateId = template.Id,
                GroupId = group.Id
            });
        }

        await context.SaveChangesAsync();

        // Apply template with ReplaceExistingGroups = true
        var service = new TemplateApplicationService(context, cacheService, tenantContext);
        var options = new TemplateApplicationOptions
        {
            ReplaceExistingPermissions = false,
            ReplaceExistingGroups = true,
            ReplaceExistingAccessDetails = false
        };

        var result = await service.ApplyTemplateAsync(
            tenantId, applicationId, userProfile.Id, template.Id, options);

        if (!result.Success)
            return false;

        // Assert: old group memberships are gone, only template-derived exist
        var remainingMemberships = await context.GroupMemberships
            .IgnoreQueryFilters()
            .Where(gm => gm.TenantId == tenantId &&
                         gm.ApplicationId == applicationId &&
                         gm.UserProfileId == userProfile.Id)
            .ToListAsync();

        // None of the existing groups should remain
        var hasOldGroups = remainingMemberships.Any(gm => existingGroupIds.Contains(gm.GroupId));
        if (hasOldGroups)
            return false;

        // All template group IDs should be present
        var remainingGroupIds = remainingMemberships.Select(gm => gm.GroupId).ToHashSet();
        var allTemplatePresent = templateGroupIds.All(id => remainingGroupIds.Contains(id));
        if (!allTemplatePresent)
            return false;

        // Count should match template count exactly
        return remainingMemberships.Count == templateCount;
    }

    [Property(MaxTest = 50)]
    public bool ReplaceAccessDetails_RemovesOldAndKeepsOnlyTemplateDerived(
        PositiveInt existingCountRaw, PositiveInt templateCountRaw)
    {
        var existingCount = (existingCountRaw.Get % 5) + 1;
        var templateCount = (templateCountRaw.Get % 5) + 1;

        return RunReplaceAccessDetailsTestAsync(existingCount, templateCount).GetAwaiter().GetResult();
    }

    private static async Task<bool> RunReplaceAccessDetailsTestAsync(int existingCount, int templateCount)
    {
        var tenantId = Guid.NewGuid();
        var applicationId = Guid.NewGuid();

        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(tenantId);
        tenantContext.ActorEmail.Returns("test@test.com");

        var cacheService = Substitute.For<ICacheService>();

        using var context = CreateDbContext(tenantContext);

        // Create user
        var userProfile = new UserProfile
        {
            TenantId = tenantId,
            ExternalSubjectId = $"ext-{Guid.NewGuid()}",
            IdentityProvider = "TestProvider",
            Email = $"user-{Guid.NewGuid()}@test.com",
            DisplayName = "Test User",
            Status = UserStatus.Active
        };
        context.UserProfiles.Add(userProfile);

        // Create existing user access details
        var existingAccessDetailKeys = new List<(string Type, string Code)>();
        for (int i = 0; i < existingCount; i++)
        {
            var type = $"ExistingType_{i}";
            var code = $"ExistingCode_{i}";
            existingAccessDetailKeys.Add((type, code));

            context.UserAccessDetails.Add(new UserAccessDetail
            {
                TenantId = tenantId,
                ApplicationId = applicationId,
                UserProfileId = userProfile.Id,
                AccessDetailType = type,
                AccessDetailCode = code,
                AccessDetailValue = $"ExistingValue_{i}",
                IsActive = true,
                ValidFrom = null,
                ValidTo = null
            });
        }

        // Create template with new access detail entries
        var template = new PermissionTemplate
        {
            TenantId = tenantId,
            ApplicationId = applicationId,
            TemplateCode = $"TPL_{Guid.NewGuid():N}"[..20].ToUpper(),
            Name = "Test Template",
            IsActive = true
        };
        context.PermissionTemplates.Add(template);

        var templateAccessDetailKeys = new List<(string Type, string Code)>();
        for (int i = 0; i < templateCount; i++)
        {
            var type = $"TemplateType_{i}";
            var code = $"TemplateCode_{i}";
            templateAccessDetailKeys.Add((type, code));

            context.PermissionTemplateAccessDetails.Add(new PermissionTemplateAccessDetail
            {
                PermissionTemplateId = template.Id,
                AccessDetailType = type,
                AccessDetailCode = code,
                AccessDetailValue = $"TemplateValue_{i}",
                Description = null,
                ValidFromOffsetDays = null,
                ValidToOffsetDays = null
            });
        }

        await context.SaveChangesAsync();

        // Apply template with ReplaceExistingAccessDetails = true
        var service = new TemplateApplicationService(context, cacheService, tenantContext);
        var options = new TemplateApplicationOptions
        {
            ReplaceExistingPermissions = false,
            ReplaceExistingGroups = false,
            ReplaceExistingAccessDetails = true
        };

        var result = await service.ApplyTemplateAsync(
            tenantId, applicationId, userProfile.Id, template.Id, options);

        if (!result.Success)
            return false;

        // Assert: old access details are gone, only template-derived exist
        var remainingDetails = await context.UserAccessDetails
            .IgnoreQueryFilters()
            .Where(ad => ad.TenantId == tenantId &&
                         ad.ApplicationId == applicationId &&
                         ad.UserProfileId == userProfile.Id)
            .ToListAsync();

        // None of the existing access detail keys should remain
        var hasOldDetails = remainingDetails.Any(ad =>
            existingAccessDetailKeys.Any(k => k.Type == ad.AccessDetailType && k.Code == ad.AccessDetailCode));
        if (hasOldDetails)
            return false;

        // All template access detail keys should be present
        var allTemplatePresent = templateAccessDetailKeys.All(k =>
            remainingDetails.Any(ad => ad.AccessDetailType == k.Type && ad.AccessDetailCode == k.Code));
        if (!allTemplatePresent)
            return false;

        // Count should match template count exactly
        return remainingDetails.Count == templateCount;
    }

    [Property(MaxTest = 50)]
    public bool ReplaceAll_RemovesAllOldAndKeepsOnlyTemplateDerived(
        PositiveInt existingPermCountRaw, PositiveInt existingGroupCountRaw, PositiveInt existingAdCountRaw,
        PositiveInt templatePermCountRaw, PositiveInt templateGroupCountRaw, PositiveInt templateAdCountRaw)
    {
        var existingPermCount = (existingPermCountRaw.Get % 3) + 1;
        var existingGroupCount = (existingGroupCountRaw.Get % 3) + 1;
        var existingAdCount = (existingAdCountRaw.Get % 3) + 1;
        var templatePermCount = (templatePermCountRaw.Get % 3) + 1;
        var templateGroupCount = (templateGroupCountRaw.Get % 3) + 1;
        var templateAdCount = (templateAdCountRaw.Get % 3) + 1;

        return RunReplaceAllTestAsync(
            existingPermCount, existingGroupCount, existingAdCount,
            templatePermCount, templateGroupCount, templateAdCount).GetAwaiter().GetResult();
    }

    private static async Task<bool> RunReplaceAllTestAsync(
        int existingPermCount, int existingGroupCount, int existingAdCount,
        int templatePermCount, int templateGroupCount, int templateAdCount)
    {
        var tenantId = Guid.NewGuid();
        var applicationId = Guid.NewGuid();

        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(tenantId);
        tenantContext.ActorEmail.Returns("test@test.com");

        var cacheService = Substitute.For<ICacheService>();

        using var context = CreateDbContext(tenantContext);

        // Create user
        var userProfile = new UserProfile
        {
            TenantId = tenantId,
            ExternalSubjectId = $"ext-{Guid.NewGuid()}",
            IdentityProvider = "TestProvider",
            Email = $"user-{Guid.NewGuid()}@test.com",
            DisplayName = "Test User",
            Status = UserStatus.Active
        };
        context.UserProfiles.Add(userProfile);

        // Shared functional area + permission type
        var functionalArea = new FunctionalArea
        {
            TenantId = tenantId,
            ApplicationId = applicationId,
            FunctionalAreaCode = $"FA_{Guid.NewGuid():N}"[..20].ToUpper(),
            Name = "Test FA",
            IsActive = true
        };
        context.FunctionalAreas.Add(functionalArea);

        var permissionType = new PermissionType
        {
            TenantId = tenantId,
            ApplicationId = applicationId,
            Code = $"PT_{Guid.NewGuid():N}"[..20].ToUpper(),
            Name = "Test PT",
            IsActive = true
        };
        context.PermissionTypes.Add(permissionType);

        // Create existing permission assignments
        var existingPermIds = new List<Guid>();
        for (int i = 0; i < existingPermCount; i++)
        {
            var perm = new Permission
            {
                TenantId = tenantId,
                ApplicationId = applicationId,
                FunctionalAreaId = functionalArea.Id,
                PermissionTypeId = permissionType.Id,
                PermissionCode = $"EXISTPERM_{Guid.NewGuid():N}"[..30].ToUpper(),
                Name = $"Existing Perm {i}",
                IsActive = true
            };
            context.Permissions.Add(perm);
            existingPermIds.Add(perm.Id);

            context.UserPermissionAssignments.Add(new UserPermissionAssignment
            {
                TenantId = tenantId,
                ApplicationId = applicationId,
                UserProfileId = userProfile.Id,
                PermissionId = perm.Id,
                Effect = Effect.Allow,
                ValidFrom = null,
                ValidTo = null
            });
        }

        // Create existing group memberships
        var existingGroupIds = new List<Guid>();
        for (int i = 0; i < existingGroupCount; i++)
        {
            var group = new Group
            {
                TenantId = tenantId,
                ApplicationId = applicationId,
                Name = $"ExistGroup-{Guid.NewGuid()}",
                IsActive = true
            };
            context.Groups.Add(group);
            existingGroupIds.Add(group.Id);

            context.GroupMemberships.Add(new GroupMembership
            {
                TenantId = tenantId,
                ApplicationId = applicationId,
                GroupId = group.Id,
                UserProfileId = userProfile.Id
            });
        }

        // Create existing access details
        var existingAdKeys = new List<(string Type, string Code)>();
        for (int i = 0; i < existingAdCount; i++)
        {
            var type = $"ExType_{i}";
            var code = $"ExCode_{i}";
            existingAdKeys.Add((type, code));

            context.UserAccessDetails.Add(new UserAccessDetail
            {
                TenantId = tenantId,
                ApplicationId = applicationId,
                UserProfileId = userProfile.Id,
                AccessDetailType = type,
                AccessDetailCode = code,
                AccessDetailValue = $"ExValue_{i}",
                IsActive = true,
                ValidFrom = null,
                ValidTo = null
            });
        }

        // Create template
        var template = new PermissionTemplate
        {
            TenantId = tenantId,
            ApplicationId = applicationId,
            TemplateCode = $"TPL_{Guid.NewGuid():N}"[..20].ToUpper(),
            Name = "Test Template",
            IsActive = true
        };
        context.PermissionTemplates.Add(template);

        // Template permissions
        var templatePermIds = new List<Guid>();
        for (int i = 0; i < templatePermCount; i++)
        {
            var perm = new Permission
            {
                TenantId = tenantId,
                ApplicationId = applicationId,
                FunctionalAreaId = functionalArea.Id,
                PermissionTypeId = permissionType.Id,
                PermissionCode = $"TPLPERM_{Guid.NewGuid():N}"[..30].ToUpper(),
                Name = $"Template Perm {i}",
                IsActive = true
            };
            context.Permissions.Add(perm);
            templatePermIds.Add(perm.Id);

            context.PermissionTemplatePermissions.Add(new PermissionTemplatePermission
            {
                PermissionTemplateId = template.Id,
                PermissionId = perm.Id,
                Effect = Effect.Allow,
                ValidFromOffsetDays = null,
                ValidToOffsetDays = null
            });
        }

        // Template groups
        var templateGroupIds = new List<Guid>();
        for (int i = 0; i < templateGroupCount; i++)
        {
            var group = new Group
            {
                TenantId = tenantId,
                ApplicationId = applicationId,
                Name = $"TplGroup-{Guid.NewGuid()}",
                IsActive = true
            };
            context.Groups.Add(group);
            templateGroupIds.Add(group.Id);

            context.PermissionTemplateGroups.Add(new PermissionTemplateGroup
            {
                PermissionTemplateId = template.Id,
                GroupId = group.Id
            });
        }

        // Template access details
        var templateAdKeys = new List<(string Type, string Code)>();
        for (int i = 0; i < templateAdCount; i++)
        {
            var type = $"TplType_{i}";
            var code = $"TplCode_{i}";
            templateAdKeys.Add((type, code));

            context.PermissionTemplateAccessDetails.Add(new PermissionTemplateAccessDetail
            {
                PermissionTemplateId = template.Id,
                AccessDetailType = type,
                AccessDetailCode = code,
                AccessDetailValue = $"TplValue_{i}",
                Description = null,
                ValidFromOffsetDays = null,
                ValidToOffsetDays = null
            });
        }

        await context.SaveChangesAsync();

        // Apply template with all replace flags
        var service = new TemplateApplicationService(context, cacheService, tenantContext);
        var options = new TemplateApplicationOptions
        {
            ReplaceExistingPermissions = true,
            ReplaceExistingGroups = true,
            ReplaceExistingAccessDetails = true
        };

        var result = await service.ApplyTemplateAsync(
            tenantId, applicationId, userProfile.Id, template.Id, options);

        if (!result.Success)
            return false;

        // Verify permissions: only template-derived remain
        var remainingPerms = await context.UserPermissionAssignments
            .IgnoreQueryFilters()
            .Where(a => a.TenantId == tenantId &&
                        a.ApplicationId == applicationId &&
                        a.UserProfileId == userProfile.Id)
            .ToListAsync();

        if (remainingPerms.Any(a => existingPermIds.Contains(a.PermissionId)))
            return false;
        if (remainingPerms.Count != templatePermCount)
            return false;
        if (!templatePermIds.All(id => remainingPerms.Any(a => a.PermissionId == id)))
            return false;

        // Verify groups: only template-derived remain
        var remainingGroups = await context.GroupMemberships
            .IgnoreQueryFilters()
            .Where(gm => gm.TenantId == tenantId &&
                         gm.ApplicationId == applicationId &&
                         gm.UserProfileId == userProfile.Id)
            .ToListAsync();

        if (remainingGroups.Any(gm => existingGroupIds.Contains(gm.GroupId)))
            return false;
        if (remainingGroups.Count != templateGroupCount)
            return false;
        if (!templateGroupIds.All(id => remainingGroups.Any(gm => gm.GroupId == id)))
            return false;

        // Verify access details: only template-derived remain
        var remainingAds = await context.UserAccessDetails
            .IgnoreQueryFilters()
            .Where(ad => ad.TenantId == tenantId &&
                         ad.ApplicationId == applicationId &&
                         ad.UserProfileId == userProfile.Id)
            .ToListAsync();

        if (remainingAds.Any(ad => existingAdKeys.Any(k => k.Type == ad.AccessDetailType && k.Code == ad.AccessDetailCode)))
            return false;
        if (remainingAds.Count != templateAdCount)
            return false;
        if (!templateAdKeys.All(k => remainingAds.Any(ad => ad.AccessDetailType == k.Type && ad.AccessDetailCode == k.Code)))
            return false;

        return true;
    }
}
