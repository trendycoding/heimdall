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
/// Property 1: Deny Overrides Allow
/// Generate arbitrary user with both Allow and Deny assignments for the same permission
/// (both temporally valid, all entities active); assert resolution always returns Deny.
///
/// **Validates: Requirements 9.5, 10.3**
/// </summary>
public class PermissionResolver_DenyOverridesAllowTests
{
    /// <summary>
    /// Creates a fresh in-memory DbContext with the specified tenant context.
    /// </summary>
    private static HeimdallDbContext CreateDbContext(ITenantContext tenantContext)
    {
        var options = new DbContextOptionsBuilder<HeimdallDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new HeimdallDbContext(options, tenantContext);
    }

    [Property(MaxTest = 100)]
    public bool DenyOverridesAllow_WithDirectAssignments_AlwaysReturnsDeny(PositiveInt allowCountRaw, PositiveInt denyCountRaw)
    {
        // Constrain to 1-5 assignments each
        var allowCount = (allowCountRaw.Get % 5) + 1;
        var denyCount = (denyCountRaw.Get % 5) + 1;

        return RunDenyOverridesAllowTestAsync(allowCount, denyCount).GetAwaiter().GetResult();
    }

    private static async Task<bool> RunDenyOverridesAllowTestAsync(int allowCount, int denyCount)
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
        var permissionCode = $"PERM_{Guid.NewGuid():N}"[..30].ToUpper();
        var permission = new Permission
        {
            TenantId = tenantId,
            ApplicationId = applicationId,
            FunctionalAreaId = functionalArea.Id,
            PermissionTypeId = permissionType.Id,
            PermissionCode = permissionCode,
            Name = "Test Permission",
            IsActive = true
        };
        context.Permissions.Add(permission);

        // Create Allow assignments (all temporally valid at evaluationTime)
        for (int i = 0; i < allowCount; i++)
        {
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
        }

        // Create Deny assignments (all temporally valid at evaluationTime)
        for (int i = 0; i < denyCount; i++)
        {
            context.UserPermissionAssignments.Add(new UserPermissionAssignment
            {
                TenantId = tenantId,
                ApplicationId = applicationId,
                UserProfileId = userProfile.Id,
                PermissionId = permission.Id,
                Effect = Effect.Deny,
                ValidFrom = evaluationTime.AddDays(-10),
                ValidTo = evaluationTime.AddDays(10)
            });
        }

        await context.SaveChangesAsync();

        var resolver = new PermissionResolver(context, NullHeimdallMetricsService.Instance);

        // Act: Check permission by code
        var result = await resolver.CheckPermissionByCodeAsync(
            tenantId, applicationId, userProfile.Id, permissionCode, evaluationTime);

        // Assert: Must always be Deny when both Allow and Deny assignments exist
        return result.Allowed == false && result.Decision == "Deny";
    }

    [Property(MaxTest = 100)]
    public bool DenyOverridesAllow_WithGroupAssignments_AlwaysReturnsDeny(PositiveInt allowCountRaw, PositiveInt denyCountRaw)
    {
        // Constrain to 1-5 assignments each
        var allowCount = (allowCountRaw.Get % 5) + 1;
        var denyCount = (denyCountRaw.Get % 5) + 1;

        return RunDenyOverridesAllowWithGroupsTestAsync(allowCount, denyCount).GetAwaiter().GetResult();
    }

    private static async Task<bool> RunDenyOverridesAllowWithGroupsTestAsync(int allowCount, int denyCount)
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

        // Active permission type
        var permissionType = new PermissionType
        {
            TenantId = tenantId,
            ApplicationId = applicationId,
            Code = $"PT_{Guid.NewGuid():N}"[..20].ToUpper(),
            Name = "Test PT",
            IsActive = true
        };
        context.PermissionTypes.Add(permissionType);

        // Active permission
        var permissionCode = $"PERM_{Guid.NewGuid():N}"[..30].ToUpper();
        var permission = new Permission
        {
            TenantId = tenantId,
            ApplicationId = applicationId,
            FunctionalAreaId = functionalArea.Id,
            PermissionTypeId = permissionType.Id,
            PermissionCode = permissionCode,
            Name = "Test Permission",
            IsActive = true
        };
        context.Permissions.Add(permission);

        // Active group
        var group = new Group
        {
            TenantId = tenantId,
            ApplicationId = applicationId,
            Name = $"Group-{Guid.NewGuid()}",
            IsActive = true
        };
        context.Groups.Add(group);

        // Group membership
        var membership = new GroupMembership
        {
            TenantId = tenantId,
            ApplicationId = applicationId,
            GroupId = group.Id,
            UserProfileId = userProfile.Id
        };
        context.GroupMemberships.Add(membership);

        // Direct Allow assignments
        for (int i = 0; i < allowCount; i++)
        {
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
        }

        // Group Deny assignments
        for (int i = 0; i < denyCount; i++)
        {
            context.GroupPermissionAssignments.Add(new GroupPermissionAssignment
            {
                TenantId = tenantId,
                ApplicationId = applicationId,
                GroupId = group.Id,
                PermissionId = permission.Id,
                Effect = Effect.Deny,
                ValidFrom = evaluationTime.AddDays(-10),
                ValidTo = evaluationTime.AddDays(10)
            });
        }

        await context.SaveChangesAsync();

        var resolver = new PermissionResolver(context, NullHeimdallMetricsService.Instance);

        // Act
        var result = await resolver.CheckPermissionByCodeAsync(
            tenantId, applicationId, userProfile.Id, permissionCode, evaluationTime);

        // Assert: Deny always wins over Allow
        return result.Allowed == false && result.Decision == "Deny";
    }

    [Property(MaxTest = 100)]
    public bool DenyOverridesAllow_MixedDirectAndGroupAssignments_AlwaysReturnsDeny(
        PositiveInt directAllowRaw, PositiveInt directDenyRaw,
        PositiveInt groupAllowRaw, PositiveInt groupDenyRaw)
    {
        // Constrain to 0-3 per source, ensuring at least one Allow and one Deny total
        var directAllow = directAllowRaw.Get % 4;
        var directDeny = directDenyRaw.Get % 4;
        var groupAllow = groupAllowRaw.Get % 4;
        var groupDeny = groupDenyRaw.Get % 4;

        var totalAllow = directAllow + groupAllow;
        var totalDeny = directDeny + groupDeny;

        // Skip cases without both Allow and Deny (vacuously true)
        if (totalAllow < 1 || totalDeny < 1)
            return true;

        return RunMixedAssignmentsTestAsync(directAllow, directDeny, groupAllow, groupDeny)
            .GetAwaiter().GetResult();
    }

    private static async Task<bool> RunMixedAssignmentsTestAsync(
        int directAllowCount, int directDenyCount, int groupAllowCount, int groupDenyCount)
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

        // Active permission type
        var permissionType = new PermissionType
        {
            TenantId = tenantId,
            ApplicationId = applicationId,
            Code = $"PT_{Guid.NewGuid():N}"[..20].ToUpper(),
            Name = "Test PT",
            IsActive = true
        };
        context.PermissionTypes.Add(permissionType);

        // Active permission
        var permissionCode = $"PERM_{Guid.NewGuid():N}"[..30].ToUpper();
        var permission = new Permission
        {
            TenantId = tenantId,
            ApplicationId = applicationId,
            FunctionalAreaId = functionalArea.Id,
            PermissionTypeId = permissionType.Id,
            PermissionCode = permissionCode,
            Name = "Test Permission",
            IsActive = true
        };
        context.Permissions.Add(permission);

        // Active group
        var group = new Group
        {
            TenantId = tenantId,
            ApplicationId = applicationId,
            Name = $"Group-{Guid.NewGuid()}",
            IsActive = true
        };
        context.Groups.Add(group);

        // Group membership
        var membership = new GroupMembership
        {
            TenantId = tenantId,
            ApplicationId = applicationId,
            GroupId = group.Id,
            UserProfileId = userProfile.Id
        };
        context.GroupMemberships.Add(membership);

        // Direct Allow assignments
        for (int i = 0; i < directAllowCount; i++)
        {
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
        }

        // Direct Deny assignments
        for (int i = 0; i < directDenyCount; i++)
        {
            context.UserPermissionAssignments.Add(new UserPermissionAssignment
            {
                TenantId = tenantId,
                ApplicationId = applicationId,
                UserProfileId = userProfile.Id,
                PermissionId = permission.Id,
                Effect = Effect.Deny,
                ValidFrom = evaluationTime.AddDays(-10),
                ValidTo = evaluationTime.AddDays(10)
            });
        }

        // Group Allow assignments
        for (int i = 0; i < groupAllowCount; i++)
        {
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
        }

        // Group Deny assignments
        for (int i = 0; i < groupDenyCount; i++)
        {
            context.GroupPermissionAssignments.Add(new GroupPermissionAssignment
            {
                TenantId = tenantId,
                ApplicationId = applicationId,
                GroupId = group.Id,
                PermissionId = permission.Id,
                Effect = Effect.Deny,
                ValidFrom = evaluationTime.AddDays(-10),
                ValidTo = evaluationTime.AddDays(10)
            });
        }

        await context.SaveChangesAsync();

        var resolver = new PermissionResolver(context, NullHeimdallMetricsService.Instance);

        // Act
        var result = await resolver.CheckPermissionByCodeAsync(
            tenantId, applicationId, userProfile.Id, permissionCode, evaluationTime);

        // Assert: Deny always overrides Allow
        return result.Allowed == false && result.Decision == "Deny";
    }
}
