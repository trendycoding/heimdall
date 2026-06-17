using Heimdall.Infrastructure.Logging;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Heimdall.Domain.Entities;
using Heimdall.Domain.Enums;
using Heimdall.Domain.Interfaces;
using Heimdall.Infrastructure.Persistence;
using Heimdall.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using ApplicationEntity = Heimdall.Domain.Entities.Application;

namespace Heimdall.Infrastructure.Tests.Properties;

/// <summary>
/// Property 2: Temporal Validity Filtering
/// Generate arbitrary assignments with ValidFrom/ValidTo ranges and random evaluation times;
/// assert only assignments where evaluationTime ∈ [ValidFrom, ValidTo] are considered.
/// 
/// **Validates: Requirements 9.4**
/// </summary>
public class PermissionResolver_TemporalValidityTests
{
    private static (HeimdallDbContext db, ITenantContext tenantContext) CreateInMemoryContext(Guid tenantId)
    {
        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(tenantId);
        tenantContext.ActorEmail.Returns("test@test.com");

        var options = new DbContextOptionsBuilder<HeimdallDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var db = new HeimdallDbContext(options, tenantContext);
        return (db, tenantContext);
    }

    /// <summary>
    /// Seeds a complete entity chain required for permission resolution:
    /// Active user, active functional area, active permission type, active permission.
    /// Returns the IDs needed for creating assignments and invoking the resolver.
    /// </summary>
    private static async Task<(Guid tenantId, Guid applicationId, Guid userProfileId, Guid permissionId, string permissionCode)>
        SeedBaseEntitiesAsync(HeimdallDbContext db, Guid tenantId)
    {
        var applicationId = Guid.NewGuid();
        var application = new ApplicationEntity
        {
            TenantId = tenantId,
            Name = "Test App",
            ClientIdentifier = "test-client",
            Status = ApplicationStatus.Active
        };
        db.Applications.Add(application);
        await db.SaveChangesAsync();
        applicationId = application.Id;

        var userProfile = new UserProfile
        {
            TenantId = tenantId,
            ExternalSubjectId = "ext-user-1",
            IdentityProvider = "TestIdP",
            Email = "user@test.com",
            DisplayName = "Test User",
            Status = UserStatus.Active
        };
        db.UserProfiles.Add(userProfile);
        await db.SaveChangesAsync();

        var functionalArea = new FunctionalArea
        {
            TenantId = tenantId,
            ApplicationId = applicationId,
            FunctionalAreaCode = "TEST_AREA",
            Name = "Test Area",
            IsActive = true
        };
        db.FunctionalAreas.Add(functionalArea);
        await db.SaveChangesAsync();

        var permissionType = new PermissionType
        {
            TenantId = tenantId,
            ApplicationId = applicationId,
            Code = "READ",
            Name = "Read",
            IsActive = true
        };
        db.PermissionTypes.Add(permissionType);
        await db.SaveChangesAsync();

        var permissionCode = "TEST_AREA_READ";
        var permission = new Permission
        {
            TenantId = tenantId,
            ApplicationId = applicationId,
            FunctionalAreaId = functionalArea.Id,
            PermissionTypeId = permissionType.Id,
            PermissionCode = permissionCode,
            Name = "Test Area Read",
            IsActive = true
        };
        db.Permissions.Add(permission);
        await db.SaveChangesAsync();

        return (tenantId, applicationId, userProfile.Id, permission.Id, permissionCode);
    }

    [Property(Arbitrary = [typeof(TemporalValidityArbitraries)], MaxTest = 50)]
    public async Task<bool> Assignment_within_time_window_is_considered(
        ValidTimeWindowScenario scenario)
    {
        // Arrange: evaluationTime is within [ValidFrom, ValidTo]
        var tenantId = Guid.NewGuid();
        var (db, _) = CreateInMemoryContext(tenantId);

        try
        {
            var (_, applicationId, userProfileId, permissionId, permissionCode) =
                await SeedBaseEntitiesAsync(db, tenantId);

            var assignment = new UserPermissionAssignment
            {
                TenantId = tenantId,
                ApplicationId = applicationId,
                UserProfileId = userProfileId,
                PermissionId = permissionId,
                Effect = Effect.Allow,
                ValidFrom = scenario.ValidFrom,
                ValidTo = scenario.ValidTo
            };
            db.UserPermissionAssignments.Add(assignment);
            await db.SaveChangesAsync();

            var resolver = new PermissionResolver(db, NullHeimdallMetricsService.Instance);

            // Act
            var result = await resolver.CheckPermissionByCodeAsync(
                tenantId, applicationId, userProfileId,
                permissionCode, scenario.EvaluationTime);

            // Assert: assignment should be considered (Allow since it's the only one)
            return result.Allowed && result.Decision == "Allow";
        }
        finally
        {
            await db.DisposeAsync();
        }
    }

    [Property(Arbitrary = [typeof(TemporalValidityArbitraries)], MaxTest = 50)]
    public async Task<bool> Assignment_outside_time_window_is_excluded(
        InvalidTimeWindowScenario scenario)
    {
        // Arrange: evaluationTime is outside [ValidFrom, ValidTo]
        var tenantId = Guid.NewGuid();
        var (db, _) = CreateInMemoryContext(tenantId);

        try
        {
            var (_, applicationId, userProfileId, permissionId, permissionCode) =
                await SeedBaseEntitiesAsync(db, tenantId);

            var assignment = new UserPermissionAssignment
            {
                TenantId = tenantId,
                ApplicationId = applicationId,
                UserProfileId = userProfileId,
                PermissionId = permissionId,
                Effect = Effect.Allow,
                ValidFrom = scenario.ValidFrom,
                ValidTo = scenario.ValidTo
            };
            db.UserPermissionAssignments.Add(assignment);
            await db.SaveChangesAsync();

            var resolver = new PermissionResolver(db, NullHeimdallMetricsService.Instance);

            // Act
            var result = await resolver.CheckPermissionByCodeAsync(
                tenantId, applicationId, userProfileId,
                permissionCode, scenario.EvaluationTime);

            // Assert: assignment should be excluded (Deny because no valid assignments)
            return !result.Allowed && result.Decision == "Deny";
        }
        finally
        {
            await db.DisposeAsync();
        }
    }

    [Property(Arbitrary = [typeof(TemporalValidityArbitraries)], MaxTest = 50)]
    public async Task<bool> Null_ValidFrom_means_valid_from_beginning_of_time(
        NullValidFromScenario scenario)
    {
        // Arrange: ValidFrom is null, so any evaluationTime <= ValidTo should be valid
        var tenantId = Guid.NewGuid();
        var (db, _) = CreateInMemoryContext(tenantId);

        try
        {
            var (_, applicationId, userProfileId, permissionId, permissionCode) =
                await SeedBaseEntitiesAsync(db, tenantId);

            var assignment = new UserPermissionAssignment
            {
                TenantId = tenantId,
                ApplicationId = applicationId,
                UserProfileId = userProfileId,
                PermissionId = permissionId,
                Effect = Effect.Allow,
                ValidFrom = null,
                ValidTo = scenario.ValidTo
            };
            db.UserPermissionAssignments.Add(assignment);
            await db.SaveChangesAsync();

            var resolver = new PermissionResolver(db, NullHeimdallMetricsService.Instance);

            // Act
            var result = await resolver.CheckPermissionByCodeAsync(
                tenantId, applicationId, userProfileId,
                permissionCode, scenario.EvaluationTime);

            // Assert: when evaluationTime <= ValidTo, assignment is valid (Allow)
            // when evaluationTime > ValidTo, assignment is excluded (Deny)
            var expectedValid = scenario.EvaluationTime <= scenario.ValidTo;
            return result.Allowed == expectedValid;
        }
        finally
        {
            await db.DisposeAsync();
        }
    }

    [Property(Arbitrary = [typeof(TemporalValidityArbitraries)], MaxTest = 50)]
    public async Task<bool> Null_ValidTo_means_valid_indefinitely(
        NullValidToScenario scenario)
    {
        // Arrange: ValidTo is null, so any evaluationTime >= ValidFrom should be valid
        var tenantId = Guid.NewGuid();
        var (db, _) = CreateInMemoryContext(tenantId);

        try
        {
            var (_, applicationId, userProfileId, permissionId, permissionCode) =
                await SeedBaseEntitiesAsync(db, tenantId);

            var assignment = new UserPermissionAssignment
            {
                TenantId = tenantId,
                ApplicationId = applicationId,
                UserProfileId = userProfileId,
                PermissionId = permissionId,
                Effect = Effect.Allow,
                ValidFrom = scenario.ValidFrom,
                ValidTo = null
            };
            db.UserPermissionAssignments.Add(assignment);
            await db.SaveChangesAsync();

            var resolver = new PermissionResolver(db, NullHeimdallMetricsService.Instance);

            // Act
            var result = await resolver.CheckPermissionByCodeAsync(
                tenantId, applicationId, userProfileId,
                permissionCode, scenario.EvaluationTime);

            // Assert: when evaluationTime >= ValidFrom, assignment is valid (Allow)
            // when evaluationTime < ValidFrom, assignment is excluded (Deny)
            var expectedValid = scenario.EvaluationTime >= scenario.ValidFrom;
            return result.Allowed == expectedValid;
        }
        finally
        {
            await db.DisposeAsync();
        }
    }
}

/// <summary>
/// Scenario where evaluationTime is within [ValidFrom, ValidTo].
/// </summary>
public record ValidTimeWindowScenario(DateTime ValidFrom, DateTime ValidTo, DateTime EvaluationTime);

/// <summary>
/// Scenario where evaluationTime is outside [ValidFrom, ValidTo].
/// </summary>
public record InvalidTimeWindowScenario(DateTime ValidFrom, DateTime ValidTo, DateTime EvaluationTime);

/// <summary>
/// Scenario where ValidFrom is null (valid from beginning of time).
/// EvaluationTime may or may not be before ValidTo.
/// </summary>
public record NullValidFromScenario(DateTime ValidTo, DateTime EvaluationTime);

/// <summary>
/// Scenario where ValidTo is null (valid indefinitely).
/// EvaluationTime may or may not be after ValidFrom.
/// </summary>
public record NullValidToScenario(DateTime ValidFrom, DateTime EvaluationTime);

public static class TemporalValidityArbitraries
{
    // Generate DateTimes within a reasonable range to avoid edge cases with DateTime min/max
    private static readonly DateTime MinDate = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime MaxDate = new(2030, 12, 31, 23, 59, 59, DateTimeKind.Utc);

    private static Gen<DateTime> GenDateTime()
    {
        var minTicks = MinDate.Ticks;
        var maxTicks = MaxDate.Ticks;
        return Gen.Choose((int)(minTicks / TimeSpan.TicksPerMinute), (int)(maxTicks / TimeSpan.TicksPerMinute))
            .Select(minutes => new DateTime((long)minutes * TimeSpan.TicksPerMinute, DateTimeKind.Utc));
    }

    public static Arbitrary<ValidTimeWindowScenario> ValidTimeWindowScenarioArbitrary()
    {
        var gen = from dt1 in GenDateTime()
                  from dt2 in GenDateTime()
                  let validFrom = dt1 < dt2 ? dt1 : dt2
                  let validTo = dt1 < dt2 ? dt2 : dt1
                  from evalTime in Gen.Choose(0, 100)
                      .Select(pct => validFrom.AddTicks((long)((validTo - validFrom).Ticks * (pct / 100.0))))
                  where evalTime >= validFrom && evalTime <= validTo
                  select new ValidTimeWindowScenario(validFrom, validTo, evalTime);

        return Arb.From(gen);
    }

    public static Arbitrary<InvalidTimeWindowScenario> InvalidTimeWindowScenarioArbitrary()
    {
        var gen = from dt1 in GenDateTime()
                  from dt2 in GenDateTime()
                  let validFrom = dt1 < dt2 ? dt1 : dt2
                  let validTo = dt1 < dt2 ? dt2 : dt1
                  from beforeOrAfter in Gen.Elements(true, false)
                  from offset in Gen.Choose(1, 525600) // 1 minute to 1 year in minutes
                  let evalTime = beforeOrAfter
                      ? validFrom.AddMinutes(-offset)
                      : validTo.AddMinutes(offset)
                  where evalTime < validFrom || evalTime > validTo
                  where evalTime >= MinDate && evalTime <= MaxDate.AddYears(1)
                  select new InvalidTimeWindowScenario(validFrom, validTo, evalTime);

        return Arb.From(gen);
    }

    public static Arbitrary<NullValidFromScenario> NullValidFromScenarioArbitrary()
    {
        var gen = from validTo in GenDateTime()
                  from evalTime in GenDateTime()
                  select new NullValidFromScenario(validTo, evalTime);

        return Arb.From(gen);
    }

    public static Arbitrary<NullValidToScenario> NullValidToScenarioArbitrary()
    {
        var gen = from validFrom in GenDateTime()
                  from evalTime in GenDateTime()
                  select new NullValidToScenario(validFrom, evalTime);

        return Arb.From(gen);
    }
}
