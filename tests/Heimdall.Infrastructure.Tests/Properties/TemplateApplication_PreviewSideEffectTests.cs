using FsCheck;
using FsCheck.Xunit;
using Heimdall.Domain.Entities;
using Heimdall.Domain.Enums;
using Heimdall.Domain.Interfaces;
using Heimdall.Domain.Models;
using Heimdall.Infrastructure.Persistence;
using Heimdall.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Heimdall.Infrastructure.Tests.Properties;

/// <summary>
/// Property 17: Template Preview Side-Effect Freedom
/// Generate template preview requests; assert no database state change occurs.
///
/// **Validates: Requirements 15.8**
/// </summary>
public class TemplateApplication_PreviewSideEffectTests
{
    private static HeimdallDbContext CreateDbContext(ITenantContext tenantContext)
    {
        var options = new DbContextOptionsBuilder<HeimdallDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new HeimdallDbContext(options, tenantContext);
    }

    [Property(MaxTest = 100)]
    public bool PreviewTemplate_DoesNotChangeDatabase_WithReplaceOptions(
        PositiveInt permCountRaw, PositiveInt groupCountRaw, PositiveInt accessDetailCountRaw, bool replacePerms, bool replaceGroups, bool replaceAccessDetails)
    {
        var permCount = (permCountRaw.Get % 4) + 1;        // 1-4 template permissions
        var groupCount = (groupCountRaw.Get % 4) + 1;      // 1-4 template groups
        var accessDetailCount = (accessDetailCountRaw.Get % 4) + 1; // 1-4 template access details

        var options = new TemplateApplicationOptions
        {
            ReplaceExistingPermissions = replacePerms,
            ReplaceExistingGroups = replaceGroups,
            ReplaceExistingAccessDetails = replaceAccessDetails
        };

        return RunPreviewSideEffectTestAsync(permCount, groupCount, accessDetailCount, options)
            .GetAwaiter().GetResult();
    }

    private static async Task<bool> RunPreviewSideEffectTestAsync(
        int templatePermissionCount, int templateGroupCount, int templateAccessDetailCount,
        TemplateApplicationOptions options)
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

        // Create functional area, permission type, and permissions
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

        // Create permissions for template entries
        var permissions = new List<Permission>();
        for (int i = 0; i < templatePermissionCount; i++)
        {
            var perm = new Permission
            {
                TenantId = tenantId,
                ApplicationId = applicationId,
                FunctionalAreaId = functionalArea.Id,
                PermissionTypeId = permissionType.Id,
                PermissionCode = $"PERM_{i}_{Guid.NewGuid():N}"[..30].ToUpper(),
                Name = $"Permission {i}",
                IsActive = true
            };
            context.Permissions.Add(perm);
            permissions.Add(perm);
        }

        // Create groups for template entries
        var groups = new List<Group>();
        for (int i = 0; i < templateGroupCount; i++)
        {
            var group = new Group
            {
                TenantId = tenantId,
                ApplicationId = applicationId,
                Name = $"Group-{i}-{Guid.NewGuid()}",
                IsActive = true
            };
            context.Groups.Add(group);
            groups.Add(group);
        }

        // Create a permission template
        var template = new PermissionTemplate
        {
            TenantId = tenantId,
            ApplicationId = applicationId,
            TemplateCode = $"TMPL_{Guid.NewGuid():N}"[..25].ToUpper(),
            Name = "Test Template",
            IsActive = true
        };
        context.PermissionTemplates.Add(template);

        // Add template permission entries
        foreach (var perm in permissions)
        {
            context.PermissionTemplatePermissions.Add(new PermissionTemplatePermission
            {
                PermissionTemplateId = template.Id,
                PermissionId = perm.Id,
                Effect = Effect.Allow,
                ValidFromOffsetDays = 0,
                ValidToOffsetDays = 30
            });
        }

        // Add template group entries
        foreach (var group in groups)
        {
            context.PermissionTemplateGroups.Add(new PermissionTemplateGroup
            {
                PermissionTemplateId = template.Id,
                GroupId = group.Id
            });
        }

        // Add template access detail entries
        for (int i = 0; i < templateAccessDetailCount; i++)
        {
            context.PermissionTemplateAccessDetails.Add(new PermissionTemplateAccessDetail
            {
                PermissionTemplateId = template.Id,
                AccessDetailType = $"AD_TYPE_{i}",
                AccessDetailCode = $"AD_CODE_{i}",
                AccessDetailValue = $"Value_{i}",
                ValidFromOffsetDays = 0,
                ValidToOffsetDays = 60
            });
        }

        // Create some existing user records (to test replace scenarios)
        var existingAssignment = new UserPermissionAssignment
        {
            TenantId = tenantId,
            ApplicationId = applicationId,
            UserProfileId = userProfile.Id,
            PermissionId = permissions[0].Id,
            Effect = Effect.Allow,
            ValidFrom = DateTime.UtcNow.AddDays(-10),
            ValidTo = DateTime.UtcNow.AddDays(10)
        };
        context.UserPermissionAssignments.Add(existingAssignment);

        var existingMembership = new GroupMembership
        {
            TenantId = tenantId,
            ApplicationId = applicationId,
            GroupId = groups[0].Id,
            UserProfileId = userProfile.Id
        };
        context.GroupMemberships.Add(existingMembership);

        var existingAccessDetail = new UserAccessDetail
        {
            TenantId = tenantId,
            ApplicationId = applicationId,
            UserProfileId = userProfile.Id,
            AccessDetailType = "EXISTING_TYPE",
            AccessDetailCode = "EXISTING_CODE",
            AccessDetailValue = "ExistingValue",
            IsActive = true
        };
        context.UserAccessDetails.Add(existingAccessDetail);

        await context.SaveChangesAsync();

        // Snapshot the state before preview
        var assignmentCountBefore = await context.UserPermissionAssignments
            .IgnoreQueryFilters()
            .CountAsync();
        var membershipCountBefore = await context.GroupMemberships
            .IgnoreQueryFilters()
            .CountAsync();
        var accessDetailCountBefore = await context.UserAccessDetails
            .IgnoreQueryFilters()
            .CountAsync();
        var templateApplicationCountBefore = await context.UserPermissionTemplateApplications
            .IgnoreQueryFilters()
            .CountAsync();

        // Act: Call PreviewTemplateAsync
        var service = new TemplateApplicationService(context, cacheService, tenantContext);
        var result = await service.PreviewTemplateAsync(
            tenantId, applicationId, userProfile.Id,
            template.Id, options);

        // Assert: Database state is UNCHANGED after preview
        var assignmentCountAfter = await context.UserPermissionAssignments
            .IgnoreQueryFilters()
            .CountAsync();
        var membershipCountAfter = await context.GroupMemberships
            .IgnoreQueryFilters()
            .CountAsync();
        var accessDetailCountAfter = await context.UserAccessDetails
            .IgnoreQueryFilters()
            .CountAsync();
        var templateApplicationCountAfter = await context.UserPermissionTemplateApplications
            .IgnoreQueryFilters()
            .CountAsync();

        var assignmentsUnchanged = assignmentCountBefore == assignmentCountAfter;
        var membershipsUnchanged = membershipCountBefore == membershipCountAfter;
        var accessDetailsUnchanged = accessDetailCountBefore == accessDetailCountAfter;
        var noNewTemplateApplications = templateApplicationCountBefore == templateApplicationCountAfter;

        return assignmentsUnchanged && membershipsUnchanged && accessDetailsUnchanged && noNewTemplateApplications;
    }

    [Property(MaxTest = 50)]
    public bool PreviewTemplate_ReturnsProjectedChanges_ButNoPersistence(
        PositiveInt permCountRaw, PositiveInt groupCountRaw)
    {
        var permCount = (permCountRaw.Get % 3) + 1;   // 1-3 permissions
        var groupCount = (groupCountRaw.Get % 3) + 1; // 1-3 groups

        return RunPreviewReturnsDataButNoPersistenceAsync(permCount, groupCount)
            .GetAwaiter().GetResult();
    }

    private static async Task<bool> RunPreviewReturnsDataButNoPersistenceAsync(
        int templatePermissionCount, int templateGroupCount)
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

        // Create functional area, permission type
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

        // Create permissions
        var permissions = new List<Permission>();
        for (int i = 0; i < templatePermissionCount; i++)
        {
            var perm = new Permission
            {
                TenantId = tenantId,
                ApplicationId = applicationId,
                FunctionalAreaId = functionalArea.Id,
                PermissionTypeId = permissionType.Id,
                PermissionCode = $"PERM_{i}_{Guid.NewGuid():N}"[..30].ToUpper(),
                Name = $"Permission {i}",
                IsActive = true
            };
            context.Permissions.Add(perm);
            permissions.Add(perm);
        }

        // Create groups
        var groups = new List<Group>();
        for (int i = 0; i < templateGroupCount; i++)
        {
            var group = new Group
            {
                TenantId = tenantId,
                ApplicationId = applicationId,
                Name = $"Group-{i}-{Guid.NewGuid()}",
                IsActive = true
            };
            context.Groups.Add(group);
            groups.Add(group);
        }

        // Create template with entries
        var template = new PermissionTemplate
        {
            TenantId = tenantId,
            ApplicationId = applicationId,
            TemplateCode = $"TMPL_{Guid.NewGuid():N}"[..25].ToUpper(),
            Name = "Test Template",
            IsActive = true
        };
        context.PermissionTemplates.Add(template);

        foreach (var perm in permissions)
        {
            context.PermissionTemplatePermissions.Add(new PermissionTemplatePermission
            {
                PermissionTemplateId = template.Id,
                PermissionId = perm.Id,
                Effect = Effect.Allow,
                ValidFromOffsetDays = 0,
                ValidToOffsetDays = 30
            });
        }

        foreach (var group in groups)
        {
            context.PermissionTemplateGroups.Add(new PermissionTemplateGroup
            {
                PermissionTemplateId = template.Id,
                GroupId = group.Id
            });
        }

        context.PermissionTemplateAccessDetails.Add(new PermissionTemplateAccessDetail
        {
            PermissionTemplateId = template.Id,
            AccessDetailType = "PREVIEW_TYPE",
            AccessDetailCode = "PREVIEW_CODE",
            AccessDetailValue = "PreviewValue"
        });

        await context.SaveChangesAsync();

        // Snapshot state
        var assignmentCountBefore = await context.UserPermissionAssignments
            .IgnoreQueryFilters()
            .CountAsync();
        var membershipCountBefore = await context.GroupMemberships
            .IgnoreQueryFilters()
            .CountAsync();
        var accessDetailCountBefore = await context.UserAccessDetails
            .IgnoreQueryFilters()
            .CountAsync();
        var templateApplicationCountBefore = await context.UserPermissionTemplateApplications
            .IgnoreQueryFilters()
            .CountAsync();

        // Act: Preview with additive mode (no replace)
        var service = new TemplateApplicationService(context, cacheService, tenantContext);
        var options = new TemplateApplicationOptions
        {
            ReplaceExistingPermissions = false,
            ReplaceExistingGroups = false,
            ReplaceExistingAccessDetails = false
        };

        var result = await service.PreviewTemplateAsync(
            tenantId, applicationId, userProfile.Id,
            template.Id, options);

        // Assert: Preview returned projected changes
        var hasProjectedData = result.PermissionChanges.Count > 0 ||
                               result.GroupChanges.Count > 0 ||
                               result.AccessDetailChanges.Count > 0;

        // Assert: No database state change
        var assignmentCountAfter = await context.UserPermissionAssignments
            .IgnoreQueryFilters()
            .CountAsync();
        var membershipCountAfter = await context.GroupMemberships
            .IgnoreQueryFilters()
            .CountAsync();
        var accessDetailCountAfter = await context.UserAccessDetails
            .IgnoreQueryFilters()
            .CountAsync();
        var templateApplicationCountAfter = await context.UserPermissionTemplateApplications
            .IgnoreQueryFilters()
            .CountAsync();

        var assignmentsUnchanged = assignmentCountBefore == assignmentCountAfter;
        var membershipsUnchanged = membershipCountBefore == membershipCountAfter;
        var accessDetailsUnchanged = accessDetailCountBefore == accessDetailCountAfter;
        var noNewTemplateApplications = templateApplicationCountBefore == templateApplicationCountAfter;

        return hasProjectedData && assignmentsUnchanged && membershipsUnchanged &&
               accessDetailsUnchanged && noNewTemplateApplications;
    }
}
