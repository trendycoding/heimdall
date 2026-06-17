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
/// Property 6: ExternalSubjectId Lookup Equivalence
/// Generate users with external IDs; assert check by external ID equals check by resolved UserProfileId.
///
/// **Validates: Requirements 10.12**
/// </summary>
public class PermissionResolver_ExternalIdLookupTests
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
    public bool ExternalIdLookup_WithDirectAssignment_EqualsUserProfileIdLookup(
        PositiveInt assignmentCountRaw, bool useAllow)
    {
        var assignmentCount = (assignmentCountRaw.Get % 5) + 1;
        var effect = useAllow ? Effect.Allow : Effect.Deny;

        return RunExternalIdEquivalenceTestAsync(assignmentCount, effect, includeGroup: false)
            .GetAwaiter().GetResult();
    }

    [Property(MaxTest = 100)]
    public bool ExternalIdLookup_WithGroupAssignment_EqualsUserProfileIdLookup(
        PositiveInt assignmentCountRaw, bool useAllow)
    {
        var assignmentCount = (assignmentCountRaw.Get % 5) + 1;
        var effect = useAllow ? Effect.Allow : Effect.Deny;

        return RunExternalIdEquivalenceTestAsync(assignmentCount, effect, includeGroup: true)
            .GetAwaiter().GetResult();
    }

    [Property(MaxTest = 100)]
    public bool ExternalIdLookup_WithNoAssignments_EqualsUserProfileIdLookup(PositiveInt seed)
    {
        return RunExternalIdEquivalenceNoAssignmentsTestAsync(seed.Get)
            .GetAwaiter().GetResult();
    }

    private static async Task<bool> RunExternalIdEquivalenceTestAsync(
        int assignmentCount, Effect effect, bool includeGroup)
    {
        var tenantId = Guid.NewGuid();
        var applicationId = Guid.NewGuid();
        var evaluationTime = DateTime.UtcNow;
        var externalSubjectId = $"ext-subject-{Guid.NewGuid()}";
        var identityProvider = $"Provider-{Guid.NewGuid()}";

        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(tenantId);
        tenantContext.ActorEmail.Returns("test@test.com");

        using var context = CreateDbContext(tenantContext);

        // Active user with a random external subject ID and identity provider
        var userProfile = new UserProfile
        {
            TenantId = tenantId,
            ExternalSubjectId = externalSubjectId,
            IdentityProvider = identityProvider,
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

        if (includeGroup)
        {
            // Active group with membership
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

            // Create group permission assignments
            for (int i = 0; i < assignmentCount; i++)
            {
                context.GroupPermissionAssignments.Add(new GroupPermissionAssignment
                {
                    TenantId = tenantId,
                    ApplicationId = applicationId,
                    GroupId = group.Id,
                    PermissionId = permission.Id,
                    Effect = effect,
                    ValidFrom = evaluationTime.AddDays(-10),
                    ValidTo = evaluationTime.AddDays(10)
                });
            }
        }
        else
        {
            // Create direct user permission assignments
            for (int i = 0; i < assignmentCount; i++)
            {
                context.UserPermissionAssignments.Add(new UserPermissionAssignment
                {
                    TenantId = tenantId,
                    ApplicationId = applicationId,
                    UserProfileId = userProfile.Id,
                    PermissionId = permission.Id,
                    Effect = effect,
                    ValidFrom = evaluationTime.AddDays(-10),
                    ValidTo = evaluationTime.AddDays(10)
                });
            }
        }

        await context.SaveChangesAsync();

        var resolver = new PermissionResolver(context, NullHeimdallMetricsService.Instance);

        // Act: Check by ExternalSubjectId
        var externalIdResult = await resolver.CheckPermissionByExternalIdAsync(
            tenantId, applicationId, externalSubjectId, identityProvider,
            permissionCode, evaluationTime);

        // Act: Check by resolved UserProfileId
        var userProfileIdResult = await resolver.CheckPermissionByCodeAsync(
            tenantId, applicationId, userProfile.Id, permissionCode, evaluationTime);

        // Assert: Both results must be identical (same Allowed, same Decision)
        return externalIdResult.Allowed == userProfileIdResult.Allowed
            && externalIdResult.Decision == userProfileIdResult.Decision;
    }

    private static async Task<bool> RunExternalIdEquivalenceNoAssignmentsTestAsync(int seed)
    {
        var tenantId = Guid.NewGuid();
        var applicationId = Guid.NewGuid();
        var evaluationTime = DateTime.UtcNow;
        var externalSubjectId = $"ext-subject-{seed}-{Guid.NewGuid()}";
        var identityProvider = $"Provider-{seed}-{Guid.NewGuid():N}";

        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(tenantId);
        tenantContext.ActorEmail.Returns("test@test.com");

        using var context = CreateDbContext(tenantContext);

        // Active user with no assignments
        var userProfile = new UserProfile
        {
            TenantId = tenantId,
            ExternalSubjectId = externalSubjectId,
            IdentityProvider = identityProvider,
            Email = $"user-{Guid.NewGuid()}@test.com",
            DisplayName = "Test User No Assignments",
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

        await context.SaveChangesAsync();

        var resolver = new PermissionResolver(context, NullHeimdallMetricsService.Instance);

        // Act: Check by ExternalSubjectId
        var externalIdResult = await resolver.CheckPermissionByExternalIdAsync(
            tenantId, applicationId, externalSubjectId, identityProvider,
            permissionCode, evaluationTime);

        // Act: Check by resolved UserProfileId
        var userProfileIdResult = await resolver.CheckPermissionByCodeAsync(
            tenantId, applicationId, userProfile.Id, permissionCode, evaluationTime);

        // Assert: Both results must be identical (same Allowed, same Decision)
        return externalIdResult.Allowed == userProfileIdResult.Allowed
            && externalIdResult.Decision == userProfileIdResult.Decision;
    }
}
