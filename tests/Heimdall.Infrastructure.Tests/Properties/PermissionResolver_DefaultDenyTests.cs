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
/// Property 4: Default Deny
/// **Validates: Requirements 10.10, 10.11**
///
/// 10.10 - IF the target UserProfile has a Status of Inactive, THEN the Platform SHALL return a Deny decision without evaluating assignments
/// 10.11 - IF no applicable permission assignments exist for the requested user and permission combination, THEN the Platform SHALL return a Deny decision by default
/// </summary>
public class PermissionResolver_DefaultDenyTests
{
    private static (HeimdallDbContext db, PermissionResolver resolver, Guid tenantId, Guid appId) CreateContext()
    {
        var tenantId = Guid.NewGuid();
        var appId = Guid.NewGuid();
        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(tenantId);
        tenantContext.ActorEmail.Returns("test@test.com");

        var options = new DbContextOptionsBuilder<HeimdallDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var db = new HeimdallDbContext(options, tenantContext);
        var resolver = new PermissionResolver(db, NullHeimdallMetricsService.Instance);
        return (db, resolver, tenantId, appId);
    }

    private static void SetEntityId<T>(T entity, Guid id) where T : BaseEntity
    {
        var prop = typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id))!;
        prop.SetValue(entity, id);
    }

    private static string ToValidPermissionCode(int seed)
    {
        // Generate deterministic but unique valid permission codes from a seed
        return $"PERM_{Math.Abs(seed):X8}";
    }

    /// <summary>
    /// **Validates: Requirements 10.10**
    /// An inactive user always receives a Deny decision immediately, without evaluating any assignments.
    /// </summary>
    [Property]
    public async Task<bool> InactiveUser_AlwaysReturnsDeny_WithoutEvaluatingAssignments(int seed)
    {
        var (db, resolver, tenantId, appId) = CreateContext();
        await using (db)
        {
            // Arrange: Create an inactive user with a valid permission and Allow assignment
            var userId = Guid.NewGuid();
            var permissionId = Guid.NewGuid();
            var faId = Guid.NewGuid();
            var ptId = Guid.NewGuid();
            var code = ToValidPermissionCode(seed);

            var user = new UserProfile
            {
                TenantId = tenantId,
                ExternalSubjectId = $"ext_{seed}",
                IdentityProvider = "TestIdP",
                Email = "inactive@test.com",
                DisplayName = "Inactive User",
                Status = UserStatus.Inactive // Key: user is inactive
            };
            SetEntityId(user, userId);

            var fa = new FunctionalArea
            {
                TenantId = tenantId,
                ApplicationId = appId,
                FunctionalAreaCode = "FA_TEST",
                Name = "Test Area",
                IsActive = true
            };
            SetEntityId(fa, faId);

            var pt = new PermissionType
            {
                TenantId = tenantId,
                ApplicationId = appId,
                Code = "READ",
                Name = "Read",
                IsActive = true
            };
            SetEntityId(pt, ptId);

            var permission = new Permission
            {
                TenantId = tenantId,
                ApplicationId = appId,
                FunctionalAreaId = faId,
                PermissionTypeId = ptId,
                PermissionCode = code,
                Name = "Test Permission",
                IsActive = true
            };
            SetEntityId(permission, permissionId);

            // Give the inactive user an Allow assignment (should be ignored)
            var assignment = new UserPermissionAssignment
            {
                TenantId = tenantId,
                ApplicationId = appId,
                UserProfileId = userId,
                PermissionId = permissionId,
                Effect = Effect.Allow,
                ValidFrom = null,
                ValidTo = null
            };
            SetEntityId(assignment, Guid.NewGuid());

            db.UserProfiles.Add(user);
            db.FunctionalAreas.Add(fa);
            db.PermissionTypes.Add(pt);
            db.Permissions.Add(permission);
            db.UserPermissionAssignments.Add(assignment);
            await db.SaveChangesAsync();

            // Act
            var result = await resolver.CheckPermissionByCodeAsync(
                tenantId, appId, userId, code);

            // Assert: Deny because user is inactive, no assignments evaluated
            return !result.Allowed
                && result.Decision == "Deny"
                && result.MatchedAssignments.Count == 0;
        }
    }

    /// <summary>
    /// **Validates: Requirements 10.11**
    /// An active user with no applicable permission assignments receives a Deny decision by default.
    /// </summary>
    [Property]
    public async Task<bool> ActiveUser_NoAssignments_ReturnsDenyByDefault(int seed)
    {
        var (db, resolver, tenantId, appId) = CreateContext();
        await using (db)
        {
            // Arrange: Create an active user but with no permission assignments at all
            var userId = Guid.NewGuid();
            var permissionId = Guid.NewGuid();
            var faId = Guid.NewGuid();
            var ptId = Guid.NewGuid();
            var code = ToValidPermissionCode(seed);

            var user = new UserProfile
            {
                TenantId = tenantId,
                ExternalSubjectId = $"ext_{seed}",
                IdentityProvider = "TestIdP",
                Email = "active@test.com",
                DisplayName = "Active User",
                Status = UserStatus.Active // Key: user is active
            };
            SetEntityId(user, userId);

            var fa = new FunctionalArea
            {
                TenantId = tenantId,
                ApplicationId = appId,
                FunctionalAreaCode = "FA_TEST",
                Name = "Test Area",
                IsActive = true
            };
            SetEntityId(fa, faId);

            var pt = new PermissionType
            {
                TenantId = tenantId,
                ApplicationId = appId,
                Code = "READ",
                Name = "Read",
                IsActive = true
            };
            SetEntityId(pt, ptId);

            var permission = new Permission
            {
                TenantId = tenantId,
                ApplicationId = appId,
                FunctionalAreaId = faId,
                PermissionTypeId = ptId,
                PermissionCode = code,
                Name = "Test Permission",
                IsActive = true
            };
            SetEntityId(permission, permissionId);

            db.UserProfiles.Add(user);
            db.FunctionalAreas.Add(fa);
            db.PermissionTypes.Add(pt);
            db.Permissions.Add(permission);
            // No assignments added - this is the key scenario
            await db.SaveChangesAsync();

            // Act
            var result = await resolver.CheckPermissionByCodeAsync(
                tenantId, appId, userId, code);

            // Assert: Deny because no assignments exist
            return !result.Allowed
                && result.Decision == "Deny"
                && result.MatchedAssignments.Count == 0;
        }
    }
}
