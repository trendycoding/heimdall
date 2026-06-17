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
/// Property 13: Access Detail Filtering by Functional Area Requirements
/// Generate access details with various types; assert only those matching active FA requirements are returned.
///
/// **Validates: Requirements 11.4, 12.4, 13.3**
/// </summary>
public class AccessDetailResolver_FilteringTests
{
    private static HeimdallDbContext CreateDbContext(ITenantContext tenantContext)
    {
        var options = new DbContextOptionsBuilder<HeimdallDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new HeimdallDbContext(options, tenantContext);
    }

    [Property(MaxTest = 100)]
    public bool OnlyAccessDetailsMatchingActiveFARequirements_AreReturned(
        PositiveInt matchingCountRaw, PositiveInt nonMatchingCountRaw, PositiveInt requirementCountRaw)
    {
        // Constrain counts to reasonable ranges
        var matchingCount = (matchingCountRaw.Get % 5) + 1;    // 1-5 matching details
        var nonMatchingCount = (nonMatchingCountRaw.Get % 5) + 1; // 1-5 non-matching details
        var requirementCount = (requirementCountRaw.Get % 3) + 1; // 1-3 FA requirements

        return RunFilteringTestAsync(matchingCount, nonMatchingCount, requirementCount)
            .GetAwaiter().GetResult();
    }

    private static async Task<bool> RunFilteringTestAsync(
        int matchingDetailCount, int nonMatchingDetailCount, int requirementCount)
    {
        var tenantId = Guid.NewGuid();
        var applicationId = Guid.NewGuid();
        var evaluationTime = DateTime.UtcNow;

        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(tenantId);
        tenantContext.ActorEmail.Returns("test@test.com");

        using var context = CreateDbContext(tenantContext);

        // Active user
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

        // Active functional area
        var functionalArea = new FunctionalArea
        {
            TenantId = tenantId,
            ApplicationId = applicationId,
            FunctionalAreaCode = $"FA_{Guid.NewGuid():N}"[..20].ToUpper(),
            Name = "Test Functional Area",
            IsActive = true
        };
        context.FunctionalAreas.Add(functionalArea);

        // Active permission type
        var permissionType = new PermissionType
        {
            TenantId = tenantId,
            ApplicationId = applicationId,
            Code = $"PT_{Guid.NewGuid():N}"[..20].ToUpper(),
            Name = "Test Permission Type",
            IsActive = true
        };
        context.PermissionTypes.Add(permissionType);

        // Active permission in the functional area
        var permission = new Permission
        {
            TenantId = tenantId,
            ApplicationId = applicationId,
            FunctionalAreaId = functionalArea.Id,
            PermissionTypeId = permissionType.Id,
            PermissionCode = $"PERM_{Guid.NewGuid():N}"[..30].ToUpper(),
            Name = "Test Permission",
            IsActive = true
        };
        context.Permissions.Add(permission);

        // User permission assignment granting access to the functional area
        context.UserPermissionAssignments.Add(new UserPermissionAssignment
        {
            TenantId = tenantId,
            ApplicationId = applicationId,
            UserProfileId = userProfile.Id,
            PermissionId = permission.Id,
            Effect = Effect.Allow,
            ValidFrom = evaluationTime.AddDays(-10),
            ValidTo = evaluationTime.AddDays(10)
        });

        // Create distinct access detail types for requirements (matching types)
        var matchingTypes = Enumerable.Range(0, requirementCount)
            .Select(i => $"MATCH_TYPE_{i}_{Guid.NewGuid():N}"[..30])
            .ToList();

        // Create distinct access detail types that do NOT match any requirement
        var nonMatchingTypes = Enumerable.Range(0, nonMatchingDetailCount)
            .Select(i => $"NOMATCH_TYPE_{i}_{Guid.NewGuid():N}"[..30])
            .ToList();

        // Create active FunctionalAreaAccessRequirements for the matching types
        foreach (var matchType in matchingTypes)
        {
            context.FunctionalAreaAccessRequirements.Add(new FunctionalAreaAccessRequirement
            {
                TenantId = tenantId,
                ApplicationId = applicationId,
                FunctionalAreaId = functionalArea.Id,
                AccessDetailType = matchType,
                IsRequired = true,
                IsActive = true
            });
        }

        // Create UserAccessDetails with matching types (should be returned)
        var expectedDetailIds = new List<Guid>();
        for (int i = 0; i < matchingDetailCount; i++)
        {
            var detail = new UserAccessDetail
            {
                TenantId = tenantId,
                ApplicationId = applicationId,
                UserProfileId = userProfile.Id,
                AccessDetailType = matchingTypes[i % matchingTypes.Count],
                AccessDetailCode = $"CODE_{i}_{Guid.NewGuid():N}"[..20],
                AccessDetailValue = $"Value_{i}",
                IsActive = true,
                ValidFrom = evaluationTime.AddDays(-5),
                ValidTo = evaluationTime.AddDays(5)
            };
            context.UserAccessDetails.Add(detail);
            expectedDetailIds.Add(detail.Id);
        }

        // Create UserAccessDetails with non-matching types (should NOT be returned)
        for (int i = 0; i < nonMatchingDetailCount; i++)
        {
            context.UserAccessDetails.Add(new UserAccessDetail
            {
                TenantId = tenantId,
                ApplicationId = applicationId,
                UserProfileId = userProfile.Id,
                AccessDetailType = nonMatchingTypes[i],
                AccessDetailCode = $"NM_CODE_{i}_{Guid.NewGuid():N}"[..20],
                AccessDetailValue = $"NonMatchValue_{i}",
                IsActive = true,
                ValidFrom = evaluationTime.AddDays(-5),
                ValidTo = evaluationTime.AddDays(5)
            });
        }

        await context.SaveChangesAsync();

        var resolver = new AccessDetailResolver(context);

        // Act
        var result = await resolver.GetAccessDetailsAsync(
            tenantId, applicationId, userProfile.Id,
            functionalArea.FunctionalAreaCode, evaluationTime);

        // Assert: Only details with matching types are returned
        var returnedIds = result.Details.Select(d => d.AccessDetailId).ToHashSet();

        // All expected matching details are present
        var allMatchingReturned = expectedDetailIds.All(id => returnedIds.Contains(id));

        // No non-matching detail types are returned
        var noNonMatchingReturned = result.Details.All(d =>
            matchingTypes.Contains(d.AccessDetailType, StringComparer.OrdinalIgnoreCase));

        // Count matches expectation
        var correctCount = result.Details.Count == matchingDetailCount;

        return allMatchingReturned && noNonMatchingReturned && correctCount;
    }

    [Property(MaxTest = 100)]
    public bool InactiveFARequirements_DoNotIncludeTheirAccessDetails(
        PositiveInt activeReqCountRaw, PositiveInt inactiveReqCountRaw)
    {
        var activeReqCount = (activeReqCountRaw.Get % 3) + 1;   // 1-3 active requirements
        var inactiveReqCount = (inactiveReqCountRaw.Get % 3) + 1; // 1-3 inactive requirements

        return RunInactiveRequirementFilterTestAsync(activeReqCount, inactiveReqCount)
            .GetAwaiter().GetResult();
    }

    private static async Task<bool> RunInactiveRequirementFilterTestAsync(
        int activeReqCount, int inactiveReqCount)
    {
        var tenantId = Guid.NewGuid();
        var applicationId = Guid.NewGuid();
        var evaluationTime = DateTime.UtcNow;

        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(tenantId);
        tenantContext.ActorEmail.Returns("test@test.com");

        using var context = CreateDbContext(tenantContext);

        // Active user
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

        // Active functional area
        var functionalArea = new FunctionalArea
        {
            TenantId = tenantId,
            ApplicationId = applicationId,
            FunctionalAreaCode = $"FA_{Guid.NewGuid():N}"[..20].ToUpper(),
            Name = "Test FA",
            IsActive = true
        };
        context.FunctionalAreas.Add(functionalArea);

        // Active permission type and permission
        var permissionType = new PermissionType
        {
            TenantId = tenantId,
            ApplicationId = applicationId,
            Code = $"PT_{Guid.NewGuid():N}"[..20].ToUpper(),
            Name = "Test PT",
            IsActive = true
        };
        context.PermissionTypes.Add(permissionType);

        var permission = new Permission
        {
            TenantId = tenantId,
            ApplicationId = applicationId,
            FunctionalAreaId = functionalArea.Id,
            PermissionTypeId = permissionType.Id,
            PermissionCode = $"PERM_{Guid.NewGuid():N}"[..30].ToUpper(),
            Name = "Test Permission",
            IsActive = true
        };
        context.Permissions.Add(permission);

        // Grant user permission to the FA
        context.UserPermissionAssignments.Add(new UserPermissionAssignment
        {
            TenantId = tenantId,
            ApplicationId = applicationId,
            UserProfileId = userProfile.Id,
            PermissionId = permission.Id,
            Effect = Effect.Allow,
            ValidFrom = evaluationTime.AddDays(-10),
            ValidTo = evaluationTime.AddDays(10)
        });

        // Create active FA requirements
        var activeTypes = new List<string>();
        for (int i = 0; i < activeReqCount; i++)
        {
            var typeName = $"ACTIVE_TYPE_{i}_{Guid.NewGuid():N}"[..30];
            activeTypes.Add(typeName);
            context.FunctionalAreaAccessRequirements.Add(new FunctionalAreaAccessRequirement
            {
                TenantId = tenantId,
                ApplicationId = applicationId,
                FunctionalAreaId = functionalArea.Id,
                AccessDetailType = typeName,
                IsRequired = true,
                IsActive = true
            });
        }

        // Create inactive FA requirements
        var inactiveTypes = new List<string>();
        for (int i = 0; i < inactiveReqCount; i++)
        {
            var typeName = $"INACTIVE_TYPE_{i}_{Guid.NewGuid():N}"[..30];
            inactiveTypes.Add(typeName);
            context.FunctionalAreaAccessRequirements.Add(new FunctionalAreaAccessRequirement
            {
                TenantId = tenantId,
                ApplicationId = applicationId,
                FunctionalAreaId = functionalArea.Id,
                AccessDetailType = typeName,
                IsRequired = true,
                IsActive = false // Inactive requirement
            });
        }

        // Create user access details for both active and inactive requirement types
        var expectedDetailIds = new List<Guid>();
        for (int i = 0; i < activeReqCount; i++)
        {
            var detail = new UserAccessDetail
            {
                TenantId = tenantId,
                ApplicationId = applicationId,
                UserProfileId = userProfile.Id,
                AccessDetailType = activeTypes[i],
                AccessDetailCode = $"CODE_A_{i}_{Guid.NewGuid():N}"[..20],
                AccessDetailValue = $"ActiveVal_{i}",
                IsActive = true,
                ValidFrom = evaluationTime.AddDays(-5),
                ValidTo = evaluationTime.AddDays(5)
            };
            context.UserAccessDetails.Add(detail);
            expectedDetailIds.Add(detail.Id);
        }

        // Access details for inactive requirements (should NOT be returned)
        for (int i = 0; i < inactiveReqCount; i++)
        {
            context.UserAccessDetails.Add(new UserAccessDetail
            {
                TenantId = tenantId,
                ApplicationId = applicationId,
                UserProfileId = userProfile.Id,
                AccessDetailType = inactiveTypes[i],
                AccessDetailCode = $"CODE_I_{i}_{Guid.NewGuid():N}"[..20],
                AccessDetailValue = $"InactiveVal_{i}",
                IsActive = true,
                ValidFrom = evaluationTime.AddDays(-5),
                ValidTo = evaluationTime.AddDays(5)
            });
        }

        await context.SaveChangesAsync();

        var resolver = new AccessDetailResolver(context);

        // Act
        var result = await resolver.GetAccessDetailsAsync(
            tenantId, applicationId, userProfile.Id,
            functionalArea.FunctionalAreaCode, evaluationTime);

        // Assert: Only details matching active requirements are returned
        var returnedIds = result.Details.Select(d => d.AccessDetailId).ToHashSet();

        // All expected active-type details are returned
        var allActiveReturned = expectedDetailIds.All(id => returnedIds.Contains(id));

        // No inactive-type details are returned
        var noInactiveReturned = result.Details.All(d =>
            activeTypes.Contains(d.AccessDetailType, StringComparer.OrdinalIgnoreCase));

        // Correct count
        var correctCount = result.Details.Count == activeReqCount;

        return allActiveReturned && noInactiveReturned && correctCount;
    }

    [Property(MaxTest = 100)]
    public bool GroupAccessDetails_AreFilteredByFARequirements(
        PositiveInt matchingGroupCountRaw, PositiveInt nonMatchingGroupCountRaw)
    {
        var matchingGroupCount = (matchingGroupCountRaw.Get % 4) + 1;    // 1-4 matching group details
        var nonMatchingGroupCount = (nonMatchingGroupCountRaw.Get % 4) + 1; // 1-4 non-matching group details

        return RunGroupFilteringTestAsync(matchingGroupCount, nonMatchingGroupCount)
            .GetAwaiter().GetResult();
    }

    private static async Task<bool> RunGroupFilteringTestAsync(
        int matchingGroupDetailCount, int nonMatchingGroupDetailCount)
    {
        var tenantId = Guid.NewGuid();
        var applicationId = Guid.NewGuid();
        var evaluationTime = DateTime.UtcNow;

        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(tenantId);
        tenantContext.ActorEmail.Returns("test@test.com");

        using var context = CreateDbContext(tenantContext);

        // Active user
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

        // Active functional area
        var functionalArea = new FunctionalArea
        {
            TenantId = tenantId,
            ApplicationId = applicationId,
            FunctionalAreaCode = $"FA_{Guid.NewGuid():N}"[..20].ToUpper(),
            Name = "Test FA",
            IsActive = true
        };
        context.FunctionalAreas.Add(functionalArea);

        // Active permission type and permission
        var permissionType = new PermissionType
        {
            TenantId = tenantId,
            ApplicationId = applicationId,
            Code = $"PT_{Guid.NewGuid():N}"[..20].ToUpper(),
            Name = "Test PT",
            IsActive = true
        };
        context.PermissionTypes.Add(permissionType);

        var permission = new Permission
        {
            TenantId = tenantId,
            ApplicationId = applicationId,
            FunctionalAreaId = functionalArea.Id,
            PermissionTypeId = permissionType.Id,
            PermissionCode = $"PERM_{Guid.NewGuid():N}"[..30].ToUpper(),
            Name = "Test Permission",
            IsActive = true
        };
        context.Permissions.Add(permission);

        // Active group with user membership
        var group = new Group
        {
            TenantId = tenantId,
            ApplicationId = applicationId,
            Name = $"Group-{Guid.NewGuid()}",
            IsActive = true
        };
        context.Groups.Add(group);

        context.GroupMemberships.Add(new GroupMembership
        {
            TenantId = tenantId,
            ApplicationId = applicationId,
            GroupId = group.Id,
            UserProfileId = userProfile.Id
        });

        // Grant permission via group assignment
        context.GroupPermissionAssignments.Add(new GroupPermissionAssignment
        {
            TenantId = tenantId,
            ApplicationId = applicationId,
            GroupId = group.Id,
            PermissionId = permission.Id,
            Effect = Effect.Allow,
            ValidFrom = evaluationTime.AddDays(-10),
            ValidTo = evaluationTime.AddDays(10)
        });

        // Create FA requirement with a specific type
        var requiredType = $"REQ_TYPE_{Guid.NewGuid():N}"[..25];
        context.FunctionalAreaAccessRequirements.Add(new FunctionalAreaAccessRequirement
        {
            TenantId = tenantId,
            ApplicationId = applicationId,
            FunctionalAreaId = functionalArea.Id,
            AccessDetailType = requiredType,
            IsRequired = true,
            IsActive = true
        });

        // Create GroupAccessDetails matching the requirement type (should be returned)
        var expectedDetailIds = new List<Guid>();
        for (int i = 0; i < matchingGroupDetailCount; i++)
        {
            var detail = new GroupAccessDetail
            {
                TenantId = tenantId,
                ApplicationId = applicationId,
                GroupId = group.Id,
                AccessDetailType = requiredType,
                AccessDetailCode = $"GCODE_{i}_{Guid.NewGuid():N}"[..20],
                AccessDetailValue = $"GroupVal_{i}",
                IsActive = true,
                ValidFrom = evaluationTime.AddDays(-5),
                ValidTo = evaluationTime.AddDays(5)
            };
            context.GroupAccessDetails.Add(detail);
            expectedDetailIds.Add(detail.Id);
        }

        // Create GroupAccessDetails NOT matching any requirement (should NOT be returned)
        for (int i = 0; i < nonMatchingGroupDetailCount; i++)
        {
            context.GroupAccessDetails.Add(new GroupAccessDetail
            {
                TenantId = tenantId,
                ApplicationId = applicationId,
                GroupId = group.Id,
                AccessDetailType = $"NOMATCH_GTYPE_{i}_{Guid.NewGuid():N}"[..30],
                AccessDetailCode = $"NM_GCODE_{i}_{Guid.NewGuid():N}"[..20],
                AccessDetailValue = $"NonMatchGroupVal_{i}",
                IsActive = true,
                ValidFrom = evaluationTime.AddDays(-5),
                ValidTo = evaluationTime.AddDays(5)
            });
        }

        await context.SaveChangesAsync();

        var resolver = new AccessDetailResolver(context);

        // Act
        var result = await resolver.GetAccessDetailsAsync(
            tenantId, applicationId, userProfile.Id,
            functionalArea.FunctionalAreaCode, evaluationTime);

        // Assert: Only group access details matching the required type are returned
        var returnedIds = result.Details.Select(d => d.AccessDetailId).ToHashSet();

        // All matching group details should be present
        var allMatchingReturned = expectedDetailIds.All(id => returnedIds.Contains(id));

        // All returned details should have the required type
        var allReturnedHaveCorrectType = result.Details.All(d =>
            string.Equals(d.AccessDetailType, requiredType, StringComparison.OrdinalIgnoreCase));

        // Correct count
        var correctCount = result.Details.Count == matchingGroupDetailCount;

        return allMatchingReturned && allReturnedHaveCorrectType && correctCount;
    }
}
