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
/// Property 14: Access Detail Source Indicator Correctness
/// Generate mixed direct and group access details; assert correct source indicators on each.
///
/// **Validates: Requirements 11.5, 12.5**
/// </summary>
public class AccessDetailResolver_SourceIndicatorTests
{
    private static HeimdallDbContext CreateDbContext(ITenantContext tenantContext)
    {
        var options = new DbContextOptionsBuilder<HeimdallDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new HeimdallDbContext(options, tenantContext);
    }

    [Property(MaxTest = 100)]
    public bool DirectUserAccessDetails_HaveSourceDirectUser(PositiveInt directCountRaw)
    {
        var directCount = (directCountRaw.Get % 5) + 1;
        return RunDirectSourceIndicatorTestAsync(directCount).GetAwaiter().GetResult();
    }

    private static async Task<bool> RunDirectSourceIndicatorTestAsync(int directCount)
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

        // Active permission for the functional area
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

        // Direct user permission assignment (so user has access to the functional area)
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

        // Functional area access requirement declaring the relevant AccessDetailType
        var accessDetailType = "REGION";
        context.FunctionalAreaAccessRequirements.Add(new FunctionalAreaAccessRequirement
        {
            TenantId = tenantId,
            ApplicationId = applicationId,
            FunctionalAreaId = functionalArea.Id,
            AccessDetailType = accessDetailType,
            IsRequired = true,
            IsActive = true
        });

        // Create direct UserAccessDetails
        for (int i = 0; i < directCount; i++)
        {
            context.UserAccessDetails.Add(new UserAccessDetail
            {
                TenantId = tenantId,
                ApplicationId = applicationId,
                UserProfileId = userProfile.Id,
                AccessDetailType = accessDetailType,
                AccessDetailCode = $"CODE_{i}",
                AccessDetailValue = $"Value_{i}",
                IsActive = true,
                ValidFrom = evaluationTime.AddDays(-10),
                ValidTo = evaluationTime.AddDays(10)
            });
        }

        await context.SaveChangesAsync();

        var resolver = new AccessDetailResolver(context);

        // Act
        var result = await resolver.GetAccessDetailsAsync(
            tenantId, applicationId, userProfile.Id, functionalArea.FunctionalAreaCode, evaluationTime);

        // Assert: All returned details should have Source = "DirectUser" and no group info
        if (result.Details.Count != directCount)
            return false;

        return result.Details.All(d =>
            d.Source == "DirectUser" &&
            d.GroupId == null &&
            d.GroupName == null);
    }

    [Property(MaxTest = 100)]
    public bool GroupAccessDetails_HaveSourceGroupWithCorrectGroupInfo(PositiveInt groupCountRaw)
    {
        var groupCount = (groupCountRaw.Get % 5) + 1;
        return RunGroupSourceIndicatorTestAsync(groupCount).GetAwaiter().GetResult();
    }

    private static async Task<bool> RunGroupSourceIndicatorTestAsync(int groupDetailCount)
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

        // Active permission for the functional area
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

        // Active group
        var groupName = $"TestGroup-{Guid.NewGuid():N}"[..30];
        var group = new Group
        {
            TenantId = tenantId,
            ApplicationId = applicationId,
            Name = groupName,
            IsActive = true
        };
        context.Groups.Add(group);

        // Group membership (user belongs to the group)
        context.GroupMemberships.Add(new GroupMembership
        {
            TenantId = tenantId,
            ApplicationId = applicationId,
            GroupId = group.Id,
            UserProfileId = userProfile.Id
        });

        // Group permission assignment (so user has access to the functional area via group)
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

        // Functional area access requirement
        var accessDetailType = "DEPARTMENT";
        context.FunctionalAreaAccessRequirements.Add(new FunctionalAreaAccessRequirement
        {
            TenantId = tenantId,
            ApplicationId = applicationId,
            FunctionalAreaId = functionalArea.Id,
            AccessDetailType = accessDetailType,
            IsRequired = true,
            IsActive = true
        });

        // Create GroupAccessDetails
        for (int i = 0; i < groupDetailCount; i++)
        {
            context.GroupAccessDetails.Add(new GroupAccessDetail
            {
                TenantId = tenantId,
                ApplicationId = applicationId,
                GroupId = group.Id,
                AccessDetailType = accessDetailType,
                AccessDetailCode = $"DEPT_{i}",
                AccessDetailValue = $"Department_{i}",
                IsActive = true,
                ValidFrom = evaluationTime.AddDays(-10),
                ValidTo = evaluationTime.AddDays(10)
            });
        }

        await context.SaveChangesAsync();

        var resolver = new AccessDetailResolver(context);

        // Act
        var result = await resolver.GetAccessDetailsAsync(
            tenantId, applicationId, userProfile.Id, functionalArea.FunctionalAreaCode, evaluationTime);

        // Assert: All returned details should have Source = "Group" with correct GroupId and GroupName
        if (result.Details.Count != groupDetailCount)
            return false;

        return result.Details.All(d =>
            d.Source == "Group" &&
            d.GroupId == group.Id &&
            d.GroupName == groupName);
    }

    [Property(MaxTest = 100)]
    public bool MixedAccessDetails_HaveCorrectSourceIndicators(
        PositiveInt directCountRaw, PositiveInt groupCountRaw)
    {
        var directCount = (directCountRaw.Get % 4) + 1;
        var groupCount = (groupCountRaw.Get % 4) + 1;

        return RunMixedSourceIndicatorTestAsync(directCount, groupCount).GetAwaiter().GetResult();
    }

    private static async Task<bool> RunMixedSourceIndicatorTestAsync(int directCount, int groupCount)
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

        // Active permission
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

        // Direct user permission assignment (gives the user functional area access)
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

        // Active group
        var groupName = $"MixedGroup-{Guid.NewGuid():N}"[..30];
        var group = new Group
        {
            TenantId = tenantId,
            ApplicationId = applicationId,
            Name = groupName,
            IsActive = true
        };
        context.Groups.Add(group);

        // Group membership
        context.GroupMemberships.Add(new GroupMembership
        {
            TenantId = tenantId,
            ApplicationId = applicationId,
            GroupId = group.Id,
            UserProfileId = userProfile.Id
        });

        // Functional area access requirement
        var accessDetailType = "COSTCENTER";
        context.FunctionalAreaAccessRequirements.Add(new FunctionalAreaAccessRequirement
        {
            TenantId = tenantId,
            ApplicationId = applicationId,
            FunctionalAreaId = functionalArea.Id,
            AccessDetailType = accessDetailType,
            IsRequired = true,
            IsActive = true
        });

        // Create direct UserAccessDetails
        var directDetailIds = new List<Guid>();
        for (int i = 0; i < directCount; i++)
        {
            var detail = new UserAccessDetail
            {
                TenantId = tenantId,
                ApplicationId = applicationId,
                UserProfileId = userProfile.Id,
                AccessDetailType = accessDetailType,
                AccessDetailCode = $"DIRECT_{i}",
                AccessDetailValue = $"DirectValue_{i}",
                IsActive = true,
                ValidFrom = evaluationTime.AddDays(-10),
                ValidTo = evaluationTime.AddDays(10)
            };
            context.UserAccessDetails.Add(detail);
            directDetailIds.Add(detail.Id);
        }

        // Create GroupAccessDetails
        var groupDetailIds = new List<Guid>();
        for (int i = 0; i < groupCount; i++)
        {
            var detail = new GroupAccessDetail
            {
                TenantId = tenantId,
                ApplicationId = applicationId,
                GroupId = group.Id,
                AccessDetailType = accessDetailType,
                AccessDetailCode = $"GROUP_{i}",
                AccessDetailValue = $"GroupValue_{i}",
                IsActive = true,
                ValidFrom = evaluationTime.AddDays(-10),
                ValidTo = evaluationTime.AddDays(10)
            };
            context.GroupAccessDetails.Add(detail);
            groupDetailIds.Add(detail.Id);
        }

        await context.SaveChangesAsync();

        var resolver = new AccessDetailResolver(context);

        // Act
        var result = await resolver.GetAccessDetailsAsync(
            tenantId, applicationId, userProfile.Id, functionalArea.FunctionalAreaCode, evaluationTime);

        // Assert total count
        if (result.Details.Count != directCount + groupCount)
            return false;

        // Assert direct details have correct source indicators
        var directResults = result.Details.Where(d => directDetailIds.Contains(d.AccessDetailId)).ToList();
        if (directResults.Count != directCount)
            return false;

        var allDirectCorrect = directResults.All(d =>
            d.Source == "DirectUser" &&
            d.GroupId == null &&
            d.GroupName == null);

        if (!allDirectCorrect)
            return false;

        // Assert group details have correct source indicators
        var groupResults = result.Details.Where(d => groupDetailIds.Contains(d.AccessDetailId)).ToList();
        if (groupResults.Count != groupCount)
            return false;

        var allGroupCorrect = groupResults.All(d =>
            d.Source == "Group" &&
            d.GroupId == group.Id &&
            d.GroupName == groupName);

        return allGroupCorrect;
    }

    [Property(MaxTest = 100)]
    public bool MultipleGroups_EachGroupDetailHasCorrectGroupIdAndName(PositiveInt groupCountRaw)
    {
        var groupCount = (groupCountRaw.Get % 3) + 2; // 2-4 groups
        return RunMultipleGroupsSourceIndicatorTestAsync(groupCount).GetAwaiter().GetResult();
    }

    private static async Task<bool> RunMultipleGroupsSourceIndicatorTestAsync(int groupCount)
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

        // Active permission
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

        // Direct user permission assignment (gives the user functional area access)
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

        // Functional area access requirement
        var accessDetailType = "LOCATION";
        context.FunctionalAreaAccessRequirements.Add(new FunctionalAreaAccessRequirement
        {
            TenantId = tenantId,
            ApplicationId = applicationId,
            FunctionalAreaId = functionalArea.Id,
            AccessDetailType = accessDetailType,
            IsRequired = true,
            IsActive = true
        });

        // Create multiple groups with memberships and access details
        var groupsWithDetails = new Dictionary<Guid, string>(); // groupId -> groupName
        var groupDetailMap = new Dictionary<Guid, Guid>(); // detailId -> groupId

        for (int g = 0; g < groupCount; g++)
        {
            var gName = $"Group_{g}_{Guid.NewGuid():N}"[..30];
            var group = new Group
            {
                TenantId = tenantId,
                ApplicationId = applicationId,
                Name = gName,
                IsActive = true
            };
            context.Groups.Add(group);

            // Group membership
            context.GroupMemberships.Add(new GroupMembership
            {
                TenantId = tenantId,
                ApplicationId = applicationId,
                GroupId = group.Id,
                UserProfileId = userProfile.Id
            });

            groupsWithDetails[group.Id] = gName;

            // One access detail per group
            var detail = new GroupAccessDetail
            {
                TenantId = tenantId,
                ApplicationId = applicationId,
                GroupId = group.Id,
                AccessDetailType = accessDetailType,
                AccessDetailCode = $"LOC_{g}",
                AccessDetailValue = $"Location_{g}",
                IsActive = true,
                ValidFrom = evaluationTime.AddDays(-10),
                ValidTo = evaluationTime.AddDays(10)
            };
            context.GroupAccessDetails.Add(detail);
            groupDetailMap[detail.Id] = group.Id;
        }

        await context.SaveChangesAsync();

        var resolver = new AccessDetailResolver(context);

        // Act
        var result = await resolver.GetAccessDetailsAsync(
            tenantId, applicationId, userProfile.Id, functionalArea.FunctionalAreaCode, evaluationTime);

        // Assert: Each detail from a group should have Source = "Group" with the correct GroupId and GroupName
        if (result.Details.Count != groupCount)
            return false;

        foreach (var entry in result.Details)
        {
            if (entry.Source != "Group")
                return false;

            if (entry.GroupId == null)
                return false;

            if (!groupsWithDetails.TryGetValue(entry.GroupId.Value, out var expectedName))
                return false;

            if (entry.GroupName != expectedName)
                return false;
        }

        return true;
    }
}
