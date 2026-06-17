using FsCheck;
using FsCheck.Xunit;
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

namespace Heimdall.Infrastructure.Tests.Properties;

/// <summary>
/// Property 15: Template Materialization Completeness
/// Generate templates with permission/group/access detail entries; assert every entry
/// produces a corresponding materialized record.
///
/// **Validates: Requirements 15.1, 15.2, 15.3**
/// </summary>
public class TemplateApplication_MaterializationTests
{
    private static HeimdallDbContext CreateDbContext(ITenantContext tenantContext)
    {
        var options = new DbContextOptionsBuilder<HeimdallDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new HeimdallDbContext(options, tenantContext);
    }

    private static ICacheService CreateCacheService()
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cache:DefaultTtlSeconds"] = "300"
            })
            .Build();

        return new InMemoryCacheService(memoryCache, config, NullLogger<InMemoryCacheService>.Instance, NullHeimdallMetricsService.Instance);
    }

    [Property(MaxTest = 100)]
    public bool EveryTemplatePermissionProducesUserPermissionAssignment(
        PositiveInt permCountRaw)
    {
        var permCount = (permCountRaw.Get % 5) + 1; // 1-5 permissions
        return RunMaterializationTestAsync(permCount, 0, 0).GetAwaiter().GetResult();
    }

    [Property(MaxTest = 100)]
    public bool EveryTemplateGroupProducesGroupMembership(
        PositiveInt groupCountRaw)
    {
        var groupCount = (groupCountRaw.Get % 5) + 1; // 1-5 groups
        return RunMaterializationTestAsync(0, groupCount, 0).GetAwaiter().GetResult();
    }

    [Property(MaxTest = 100)]
    public bool EveryTemplateAccessDetailProducesUserAccessDetail(
        PositiveInt adCountRaw)
    {
        var adCount = (adCountRaw.Get % 5) + 1; // 1-5 access details
        return RunMaterializationTestAsync(0, 0, adCount).GetAwaiter().GetResult();
    }

    [Property(MaxTest = 100)]
    public bool AllTemplateEntriesMaterializeCompletely(
        PositiveInt permCountRaw, PositiveInt groupCountRaw, PositiveInt adCountRaw)
    {
        var permCount = (permCountRaw.Get % 5) + 1; // 1-5 permissions
        var groupCount = (groupCountRaw.Get % 5) + 1; // 1-5 groups
        var adCount = (adCountRaw.Get % 5) + 1; // 1-5 access details
        return RunMaterializationTestAsync(permCount, groupCount, adCount).GetAwaiter().GetResult();
    }

    private static async Task<bool> RunMaterializationTestAsync(
        int permissionCount, int groupCount, int accessDetailCount)
    {
        var tenantId = Guid.NewGuid();
        var applicationId = Guid.NewGuid();

        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(tenantId);
        tenantContext.ActorEmail.Returns("test@test.com");

        using var context = CreateDbContext(tenantContext);
        var cacheService = CreateCacheService();

        // Create active user
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

        // Create active template
        var template = new PermissionTemplate
        {
            TenantId = tenantId,
            ApplicationId = applicationId,
            TemplateCode = $"TPL_{Guid.NewGuid():N}"[..20].ToUpper(),
            Name = "Test Template",
            IsActive = true
        };
        context.PermissionTemplates.Add(template);

        // Create permissions and template permission entries
        var expectedPermissionIds = new List<(Guid PermissionId, Effect Effect)>();
        for (int i = 0; i < permissionCount; i++)
        {
            var fa = new FunctionalArea
            {
                TenantId = tenantId,
                ApplicationId = applicationId,
                FunctionalAreaCode = $"FA_{Guid.NewGuid():N}"[..20].ToUpper(),
                Name = $"FA {i}",
                IsActive = true
            };
            context.FunctionalAreas.Add(fa);

            var pt = new PermissionType
            {
                TenantId = tenantId,
                ApplicationId = applicationId,
                Code = $"PT_{Guid.NewGuid():N}"[..20].ToUpper(),
                Name = $"PT {i}",
                IsActive = true
            };
            context.PermissionTypes.Add(pt);

            var permission = new Permission
            {
                TenantId = tenantId,
                ApplicationId = applicationId,
                FunctionalAreaId = fa.Id,
                PermissionTypeId = pt.Id,
                PermissionCode = $"PERM_{Guid.NewGuid():N}"[..30].ToUpper(),
                Name = $"Permission {i}",
                IsActive = true
            };
            context.Permissions.Add(permission);

            var effect = i % 2 == 0 ? Effect.Allow : Effect.Deny;
            var templatePerm = new PermissionTemplatePermission
            {
                PermissionTemplateId = template.Id,
                PermissionId = permission.Id,
                Effect = effect,
                ValidFromOffsetDays = null,
                ValidToOffsetDays = null
            };
            context.PermissionTemplatePermissions.Add(templatePerm);
            expectedPermissionIds.Add((permission.Id, effect));
        }

        // Create groups and template group entries
        var expectedGroupIds = new List<Guid>();
        for (int i = 0; i < groupCount; i++)
        {
            var group = new Group
            {
                TenantId = tenantId,
                ApplicationId = applicationId,
                Name = $"Group-{Guid.NewGuid()}",
                IsActive = true
            };
            context.Groups.Add(group);

            var templateGroup = new PermissionTemplateGroup
            {
                PermissionTemplateId = template.Id,
                GroupId = group.Id
            };
            context.PermissionTemplateGroups.Add(templateGroup);
            expectedGroupIds.Add(group.Id);
        }

        // Create template access detail entries
        var expectedAccessDetails = new List<(string Type, string Code, string Value)>();
        for (int i = 0; i < accessDetailCount; i++)
        {
            var adType = $"TYPE_{i}_{Guid.NewGuid():N}"[..15];
            var adCode = $"CODE_{i}_{Guid.NewGuid():N}"[..15];
            var adValue = $"VALUE_{i}";

            var templateAd = new PermissionTemplateAccessDetail
            {
                PermissionTemplateId = template.Id,
                AccessDetailType = adType,
                AccessDetailCode = adCode,
                AccessDetailValue = adValue,
                Description = $"Test AD {i}",
                ValidFromOffsetDays = null,
                ValidToOffsetDays = null
            };
            context.PermissionTemplateAccessDetails.Add(templateAd);
            expectedAccessDetails.Add((adType, adCode, adValue));
        }

        await context.SaveChangesAsync();

        // Act: Apply template with replace = true to avoid duplicate-skipping
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

        // Assert: Verify count matches
        if (result.PermissionsApplied != permissionCount)
            return false;
        if (result.GroupMembershipsApplied != groupCount)
            return false;
        if (result.AccessDetailsApplied != accessDetailCount)
            return false;

        // Assert: Each PermissionTemplatePermission → UserPermissionAssignment
        var materializedAssignments = await context.UserPermissionAssignments
            .IgnoreQueryFilters()
            .Where(a =>
                a.TenantId == tenantId &&
                a.ApplicationId == applicationId &&
                a.UserProfileId == userProfile.Id)
            .ToListAsync();

        if (materializedAssignments.Count != permissionCount)
            return false;

        foreach (var (permId, effect) in expectedPermissionIds)
        {
            if (!materializedAssignments.Any(a => a.PermissionId == permId && a.Effect == effect))
                return false;
        }

        // Assert: Each PermissionTemplateGroup → GroupMembership
        var materializedMemberships = await context.GroupMemberships
            .IgnoreQueryFilters()
            .Where(gm =>
                gm.TenantId == tenantId &&
                gm.ApplicationId == applicationId &&
                gm.UserProfileId == userProfile.Id)
            .ToListAsync();

        if (materializedMemberships.Count != groupCount)
            return false;

        foreach (var groupId in expectedGroupIds)
        {
            if (!materializedMemberships.Any(gm => gm.GroupId == groupId))
                return false;
        }

        // Assert: Each PermissionTemplateAccessDetail → UserAccessDetail
        var materializedAccessDetails = await context.UserAccessDetails
            .IgnoreQueryFilters()
            .Where(ad =>
                ad.TenantId == tenantId &&
                ad.ApplicationId == applicationId &&
                ad.UserProfileId == userProfile.Id)
            .ToListAsync();

        if (materializedAccessDetails.Count != accessDetailCount)
            return false;

        foreach (var (adType, adCode, adValue) in expectedAccessDetails)
        {
            if (!materializedAccessDetails.Any(ad =>
                ad.AccessDetailType == adType &&
                ad.AccessDetailCode == adCode &&
                ad.AccessDetailValue == adValue))
                return false;
        }

        return true;
    }
}
