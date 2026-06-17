using Heimdall.Infrastructure.Logging;
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
/// Property 5: Batch Permission Check Consistency
/// Generate batch of permission checks; assert each result equals the standalone single check result.
///
/// **Validates: Requirements 10.5**
/// </summary>
public class PermissionResolver_BatchConsistencyTests
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

    [Property(MaxTest = 50)]
    public bool BatchResults_MatchStandaloneResults_ForRandomPermissions(PositiveInt permissionCountRaw, PositiveInt seedRaw)
    {
        // Generate 1-10 permissions
        var permissionCount = (permissionCountRaw.Get % 10) + 1;
        var seed = seedRaw.Get;

        return RunBatchConsistencyTestAsync(permissionCount, seed).GetAwaiter().GetResult();
    }

    private static async Task<bool> RunBatchConsistencyTestAsync(int permissionCount, int seed)
    {
        var tenantId = Guid.NewGuid();
        var applicationId = Guid.NewGuid();
        var evaluationTime = DateTime.UtcNow;
        var rng = new Random(seed);

        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(tenantId);
        tenantContext.ActorEmail.Returns("test@test.com");

        // Use a named database so both resolver instances share the same data
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<HeimdallDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        // Seed data in one context
        using (var context = new HeimdallDbContext(options, tenantContext))
        {
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

            // Active group with membership
            var group = new Group
            {
                TenantId = tenantId,
                ApplicationId = applicationId,
                Name = $"Group-{Guid.NewGuid()}",
                IsActive = true
            };
            context.Groups.Add(group);

            var membership = new GroupMembership
            {
                TenantId = tenantId,
                ApplicationId = applicationId,
                GroupId = group.Id,
                UserProfileId = userProfile.Id
            };
            context.GroupMemberships.Add(membership);

            // Create multiple permissions with random assignments
            var permissionCodes = new List<string>();

            for (int i = 0; i < permissionCount; i++)
            {
                var functionalArea = new FunctionalArea
                {
                    TenantId = tenantId,
                    ApplicationId = applicationId,
                    FunctionalAreaCode = $"FA_{i}_{Guid.NewGuid():N}"[..20].ToUpper(),
                    Name = $"Functional Area {i}",
                    IsActive = true
                };
                context.FunctionalAreas.Add(functionalArea);

                var permissionType = new PermissionType
                {
                    TenantId = tenantId,
                    ApplicationId = applicationId,
                    Code = $"PT_{i}_{Guid.NewGuid():N}"[..20].ToUpper(),
                    Name = $"Permission Type {i}",
                    IsActive = true
                };
                context.PermissionTypes.Add(permissionType);

                var permCode = $"PERM_{i}_{Guid.NewGuid():N}"[..30].ToUpper();
                permissionCodes.Add(permCode);

                var permission = new Permission
                {
                    TenantId = tenantId,
                    ApplicationId = applicationId,
                    FunctionalAreaId = functionalArea.Id,
                    PermissionTypeId = permissionType.Id,
                    PermissionCode = permCode,
                    Name = $"Permission {i}",
                    IsActive = true
                };
                context.Permissions.Add(permission);

                // Randomly assign direct user permission (Allow or Deny or none)
                var directAssignmentChoice = rng.Next(3); // 0=none, 1=Allow, 2=Deny
                if (directAssignmentChoice > 0)
                {
                    context.UserPermissionAssignments.Add(new UserPermissionAssignment
                    {
                        TenantId = tenantId,
                        ApplicationId = applicationId,
                        UserProfileId = userProfile.Id,
                        PermissionId = permission.Id,
                        Effect = directAssignmentChoice == 1 ? Effect.Allow : Effect.Deny,
                        ValidFrom = evaluationTime.AddDays(-10),
                        ValidTo = evaluationTime.AddDays(10)
                    });
                }

                // Randomly assign group permission (Allow or Deny or none)
                var groupAssignmentChoice = rng.Next(3); // 0=none, 1=Allow, 2=Deny
                if (groupAssignmentChoice > 0)
                {
                    context.GroupPermissionAssignments.Add(new GroupPermissionAssignment
                    {
                        TenantId = tenantId,
                        ApplicationId = applicationId,
                        GroupId = group.Id,
                        PermissionId = permission.Id,
                        Effect = groupAssignmentChoice == 1 ? Effect.Allow : Effect.Deny,
                        ValidFrom = evaluationTime.AddDays(-10),
                        ValidTo = evaluationTime.AddDays(10)
                    });
                }
            }

            await context.SaveChangesAsync();

            // Now perform batch check and individual checks using same context
            var resolver = new PermissionResolver(context, NullHeimdallMetricsService.Instance);

            // Build batch request
            var checks = permissionCodes.Select(code => new PermissionCheckRequest
            {
                PermissionCode = code
            }).ToList();

            // Execute batch
            var batchResult = await resolver.CheckBatchAsync(
                tenantId, applicationId, userProfile.Id, checks, evaluationTime);

            // Execute individual checks
            var standaloneResults = new List<PermissionCheckResult>();
            foreach (var code in permissionCodes)
            {
                var individualResult = await resolver.CheckPermissionByCodeAsync(
                    tenantId, applicationId, userProfile.Id, code, evaluationTime);
                standaloneResults.Add(individualResult);
            }

            // Assert each batch result matches the corresponding standalone result
            if (batchResult.Results.Count != standaloneResults.Count)
                return false;

            for (int i = 0; i < batchResult.Results.Count; i++)
            {
                var batchItem = batchResult.Results[i];
                var standaloneItem = standaloneResults[i];

                if (batchItem.Allowed != standaloneItem.Allowed)
                    return false;

                if (batchItem.Decision != standaloneItem.Decision)
                    return false;
            }

            return true;
        }
    }

    [Property(MaxTest = 50)]
    public bool BatchResults_WithMixedAssignments_MatchStandaloneResults(
        PositiveInt permissionCountRaw, PositiveInt additionalAssignmentsRaw, PositiveInt seedRaw)
    {
        // Generate 2-8 permissions with extra assignments
        var permissionCount = (permissionCountRaw.Get % 7) + 2;
        var additionalAssignments = (additionalAssignmentsRaw.Get % 3) + 1;
        var seed = seedRaw.Get;

        return RunBatchWithMultipleAssignmentsTestAsync(permissionCount, additionalAssignments, seed)
            .GetAwaiter().GetResult();
    }

    private static async Task<bool> RunBatchWithMultipleAssignmentsTestAsync(
        int permissionCount, int additionalAssignmentsPerPerm, int seed)
    {
        var tenantId = Guid.NewGuid();
        var applicationId = Guid.NewGuid();
        var evaluationTime = DateTime.UtcNow;
        var rng = new Random(seed);

        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(tenantId);
        tenantContext.ActorEmail.Returns("test@test.com");

        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<HeimdallDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        using (var context = new HeimdallDbContext(options, tenantContext))
        {
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

            // Multiple groups
            var groups = new List<Group>();
            for (int g = 0; g < 3; g++)
            {
                var group = new Group
                {
                    TenantId = tenantId,
                    ApplicationId = applicationId,
                    Name = $"Group-{g}-{Guid.NewGuid()}",
                    IsActive = true
                };
                context.Groups.Add(group);
                groups.Add(group);

                context.GroupMemberships.Add(new GroupMembership
                {
                    TenantId = tenantId,
                    ApplicationId = applicationId,
                    GroupId = group.Id,
                    UserProfileId = userProfile.Id
                });
            }

            var permissionCodes = new List<string>();

            for (int i = 0; i < permissionCount; i++)
            {
                var functionalArea = new FunctionalArea
                {
                    TenantId = tenantId,
                    ApplicationId = applicationId,
                    FunctionalAreaCode = $"FA_{i}_{Guid.NewGuid():N}"[..20].ToUpper(),
                    Name = $"FA {i}",
                    IsActive = true
                };
                context.FunctionalAreas.Add(functionalArea);

                var permissionType = new PermissionType
                {
                    TenantId = tenantId,
                    ApplicationId = applicationId,
                    Code = $"PT_{i}_{Guid.NewGuid():N}"[..20].ToUpper(),
                    Name = $"PT {i}",
                    IsActive = true
                };
                context.PermissionTypes.Add(permissionType);

                var permCode = $"PERM_{i}_{Guid.NewGuid():N}"[..30].ToUpper();
                permissionCodes.Add(permCode);

                var permission = new Permission
                {
                    TenantId = tenantId,
                    ApplicationId = applicationId,
                    FunctionalAreaId = functionalArea.Id,
                    PermissionTypeId = permissionType.Id,
                    PermissionCode = permCode,
                    Name = $"Permission {i}",
                    IsActive = true
                };
                context.Permissions.Add(permission);

                // Add multiple random assignments (direct and group) for richer scenarios
                for (int a = 0; a <= additionalAssignmentsPerPerm; a++)
                {
                    var effect = rng.Next(2) == 0 ? Effect.Allow : Effect.Deny;
                    var useGroup = rng.Next(2) == 0;

                    if (useGroup)
                    {
                        var targetGroup = groups[rng.Next(groups.Count)];
                        context.GroupPermissionAssignments.Add(new GroupPermissionAssignment
                        {
                            TenantId = tenantId,
                            ApplicationId = applicationId,
                            GroupId = targetGroup.Id,
                            PermissionId = permission.Id,
                            Effect = effect,
                            ValidFrom = evaluationTime.AddDays(-10),
                            ValidTo = evaluationTime.AddDays(10)
                        });
                    }
                    else
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
            }

            await context.SaveChangesAsync();

            var resolver = new PermissionResolver(context, NullHeimdallMetricsService.Instance);

            // Build batch request
            var checks = permissionCodes.Select(code => new PermissionCheckRequest
            {
                PermissionCode = code
            }).ToList();

            // Execute batch
            var batchResult = await resolver.CheckBatchAsync(
                tenantId, applicationId, userProfile.Id, checks, evaluationTime);

            // Execute individual checks
            var standaloneResults = new List<PermissionCheckResult>();
            foreach (var code in permissionCodes)
            {
                var individualResult = await resolver.CheckPermissionByCodeAsync(
                    tenantId, applicationId, userProfile.Id, code, evaluationTime);
                standaloneResults.Add(individualResult);
            }

            // Assert consistency: batch result must match standalone result
            if (batchResult.Results.Count != standaloneResults.Count)
                return false;

            for (int i = 0; i < batchResult.Results.Count; i++)
            {
                var batchItem = batchResult.Results[i];
                var standaloneItem = standaloneResults[i];

                if (batchItem.Allowed != standaloneItem.Allowed)
                    return false;

                if (batchItem.Decision != standaloneItem.Decision)
                    return false;
            }

            return true;
        }
    }
}
