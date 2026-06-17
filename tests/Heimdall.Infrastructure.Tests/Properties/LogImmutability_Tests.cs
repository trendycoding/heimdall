using FsCheck;
using FsCheck.Xunit;
using Heimdall.Domain.Entities;
using Heimdall.Domain.Interfaces;
using Heimdall.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;

namespace Heimdall.Infrastructure.Tests.Properties;

/// <summary>
/// Property 21: Log Immutability
/// Attempt to modify/delete existing ApiCallLog and AuditLog records; assert all attempts
/// are rejected at the DbContext level via InvalidOperationException.
///
/// **Validates: Requirements 16.3, 17.4**
/// </summary>
public class LogImmutability_Tests
{
    private static (HeimdallDbContext context, ITenantContext tenantContext) CreateDbContext(
        string? dbName = null, Guid? tenantId = null)
    {
        var tid = tenantId ?? Guid.NewGuid();
        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(tid);
        tenantContext.ActorEmail.Returns("system@test.com");

        var options = new DbContextOptionsBuilder<HeimdallDbContext>()
            .UseInMemoryDatabase(databaseName: dbName ?? Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return (new HeimdallDbContext(options, tenantContext), tenantContext);
    }

    // ─── AuditLog Modification Rejected ─────────────────────────────────────────

    /// <summary>
    /// Validates: Requirement 17.4
    /// AuditLog records cannot be modified after creation. Any modification attempt
    /// must throw InvalidOperationException.
    /// </summary>
    [Property(MaxTest = 100)]
    public bool AuditLog_ModificationIsRejected(
        NonEmptyString originalActionRaw,
        NonEmptyString newActionRaw)
    {
        var originalAction = originalActionRaw.Get[..Math.Min(originalActionRaw.Get.Length, 50)];
        var newAction = newActionRaw.Get[..Math.Min(newActionRaw.Get.Length, 50)];

        return RunAuditLogModificationRejectedAsync(originalAction, newAction)
            .GetAwaiter().GetResult();
    }

    private static async Task<bool> RunAuditLogModificationRejectedAsync(
        string originalAction, string newAction)
    {
        var tenantId = Guid.NewGuid();
        var dbName = Guid.NewGuid().ToString();

        // Create and persist the audit log
        Guid auditLogId;
        {
            var (context, _) = CreateDbContext(dbName, tenantId);
            using (context)
            {
                var auditLog = new AuditLog
                {
                    TenantId = tenantId,
                    CorrelationId = Guid.NewGuid(),
                    EntityType = "TestEntity",
                    EntityId = Guid.NewGuid().ToString(),
                    Action = originalAction,
                    BeforeJson = null,
                    AfterJson = "{\"created\": true}",
                    ActorSubjectId = "sub-immutable",
                    ActorEmail = "immutable@test.com"
                };
                context.AuditLogs.Add(auditLog);
                await context.SaveChangesAsync();
                auditLogId = auditLog.Id;
            }
        }

        // Attempt to modify the audit log - should throw InvalidOperationException
        {
            var (context, _) = CreateDbContext(dbName, tenantId);
            using (context)
            {
                var auditLog = await context.AuditLogs
                    .IgnoreQueryFilters()
                    .FirstAsync(a => a.Id == auditLogId);

                auditLog.Action = newAction;

                try
                {
                    await context.SaveChangesAsync();
                    return false; // Should have thrown
                }
                catch (InvalidOperationException ex)
                {
                    return ex.Message.Contains("AuditLog records are immutable");
                }
            }
        }
    }

    // ─── AuditLog Deletion Rejected ─────────────────────────────────────────────

    /// <summary>
    /// Validates: Requirement 17.4
    /// AuditLog records cannot be deleted after creation. Any deletion attempt
    /// must throw InvalidOperationException.
    /// </summary>
    [Property(MaxTest = 100)]
    public bool AuditLog_DeletionIsRejected(NonEmptyString entityTypeRaw)
    {
        var entityType = entityTypeRaw.Get[..Math.Min(entityTypeRaw.Get.Length, 50)];
        return RunAuditLogDeletionRejectedAsync(entityType)
            .GetAwaiter().GetResult();
    }

    private static async Task<bool> RunAuditLogDeletionRejectedAsync(string entityType)
    {
        var tenantId = Guid.NewGuid();
        var dbName = Guid.NewGuid().ToString();

        Guid auditLogId;
        {
            var (context, _) = CreateDbContext(dbName, tenantId);
            using (context)
            {
                var auditLog = new AuditLog
                {
                    TenantId = tenantId,
                    CorrelationId = Guid.NewGuid(),
                    EntityType = entityType,
                    EntityId = Guid.NewGuid().ToString(),
                    Action = "Create",
                    BeforeJson = null,
                    AfterJson = "{\"test\": true}",
                    ActorSubjectId = "sub-del-test",
                    ActorEmail = "deltest@test.com"
                };
                context.AuditLogs.Add(auditLog);
                await context.SaveChangesAsync();
                auditLogId = auditLog.Id;
            }
        }

        // Attempt to delete - should throw InvalidOperationException
        {
            var (context, _) = CreateDbContext(dbName, tenantId);
            using (context)
            {
                var auditLog = await context.AuditLogs
                    .IgnoreQueryFilters()
                    .FirstAsync(a => a.Id == auditLogId);

                context.AuditLogs.Remove(auditLog);

                try
                {
                    await context.SaveChangesAsync();
                    return false; // Should have thrown
                }
                catch (InvalidOperationException ex)
                {
                    return ex.Message.Contains("AuditLog records are immutable") &&
                           ex.Message.Contains("cannot be deleted");
                }
            }
        }
    }

    // ─── ApiCallLog Deletion Rejected ───────────────────────────────────────────

    /// <summary>
    /// Validates: Requirement 16.3
    /// ApiCallLog records cannot be deleted after creation. Any deletion attempt
    /// must throw InvalidOperationException.
    /// </summary>
    [Property(MaxTest = 100)]
    public bool ApiCallLog_DeletionIsRejected(NonEmptyString endpointRaw)
    {
        var endpoint = endpointRaw.Get[..Math.Min(endpointRaw.Get.Length, 100)];
        return RunApiCallLogDeletionRejectedAsync(endpoint)
            .GetAwaiter().GetResult();
    }

    private static async Task<bool> RunApiCallLogDeletionRejectedAsync(string endpoint)
    {
        var tenantId = Guid.NewGuid();
        var dbName = Guid.NewGuid().ToString();

        Guid logId;
        {
            var (context, _) = CreateDbContext(dbName, tenantId);
            using (context)
            {
                var apiCallLog = new ApiCallLog
                {
                    TenantId = tenantId,
                    CorrelationId = Guid.NewGuid(),
                    RequestId = Guid.NewGuid().ToString(),
                    HttpMethod = "POST",
                    Endpoint = endpoint,
                    RequestPath = $"/api/{endpoint}",
                    StatusCode = 201,
                    DurationMs = 100,
                    RequestTimestamp = DateTime.UtcNow,
                    ResponseTimestamp = DateTime.UtcNow.AddMilliseconds(100)
                };
                context.ApiCallLogs.Add(apiCallLog);
                await context.SaveChangesAsync();
                logId = apiCallLog.Id;
            }
        }

        // Attempt to delete - should throw InvalidOperationException
        {
            var (context, _) = CreateDbContext(dbName, tenantId);
            using (context)
            {
                var log = await context.ApiCallLogs
                    .IgnoreQueryFilters()
                    .FirstAsync(l => l.Id == logId);

                context.ApiCallLogs.Remove(log);

                try
                {
                    await context.SaveChangesAsync();
                    return false; // Should have thrown
                }
                catch (InvalidOperationException ex)
                {
                    return ex.Message.Contains("ApiCallLog records are immutable") &&
                           ex.Message.Contains("cannot be deleted");
                }
            }
        }
    }

    // ─── ApiCallLog Non-Completion Modification Rejected ────────────────────────

    /// <summary>
    /// Validates: Requirement 16.3
    /// ApiCallLog records are immutable - modifications to non-completion fields
    /// (e.g., Endpoint, HttpMethod, RequestPath) must be rejected.
    /// </summary>
    [Property(MaxTest = 100)]
    public bool ApiCallLog_NonCompletionModificationIsRejected(
        NonEmptyString originalEndpointRaw,
        NonEmptyString newEndpointRaw)
    {
        var originalEndpoint = originalEndpointRaw.Get[..Math.Min(originalEndpointRaw.Get.Length, 100)];
        var newEndpoint = newEndpointRaw.Get[..Math.Min(newEndpointRaw.Get.Length, 100)];

        // If the values are the same, EF Core won't detect a modification - not a valid test case
        if (originalEndpoint == newEndpoint)
            return true;

        return RunApiCallLogNonCompletionModificationRejectedAsync(originalEndpoint, newEndpoint)
            .GetAwaiter().GetResult();
    }

    private static async Task<bool> RunApiCallLogNonCompletionModificationRejectedAsync(
        string originalEndpoint, string newEndpoint)
    {
        var tenantId = Guid.NewGuid();
        var dbName = Guid.NewGuid().ToString();

        Guid logId;
        {
            var (context, _) = CreateDbContext(dbName, tenantId);
            using (context)
            {
                var apiCallLog = new ApiCallLog
                {
                    TenantId = tenantId,
                    CorrelationId = Guid.NewGuid(),
                    RequestId = Guid.NewGuid().ToString(),
                    HttpMethod = "GET",
                    Endpoint = originalEndpoint,
                    RequestPath = $"/api/{originalEndpoint}",
                    StatusCode = 200,
                    DurationMs = 42,
                    RequestTimestamp = DateTime.UtcNow,
                    ResponseTimestamp = DateTime.UtcNow.AddMilliseconds(42)
                };
                context.ApiCallLogs.Add(apiCallLog);
                await context.SaveChangesAsync();
                logId = apiCallLog.Id;
            }
        }

        // Attempt to modify a non-completion field (Endpoint) - should throw
        {
            var (context, _) = CreateDbContext(dbName, tenantId);
            using (context)
            {
                var log = await context.ApiCallLogs
                    .IgnoreQueryFilters()
                    .FirstAsync(l => l.Id == logId);

                log.Endpoint = newEndpoint;

                try
                {
                    await context.SaveChangesAsync();
                    return false; // Should have thrown
                }
                catch (InvalidOperationException ex)
                {
                    return ex.Message.Contains("ApiCallLog records are immutable") &&
                           ex.Message.Contains("Endpoint");
                }
            }
        }
    }

    // ─── ApiCallLog Completion Fields Allowed ───────────────────────────────────

    /// <summary>
    /// Validates: Requirement 16.3
    /// ApiCallLog completion fields (StatusCode, DurationMs, ResponseTimestamp) are
    /// set by ApiCallLogService.CompleteLogAsync and must be allowed as they are part
    /// of the creation lifecycle.
    /// </summary>
    [Property(MaxTest = 100)]
    public bool ApiCallLog_CompletionFieldModificationIsAllowed(PositiveInt statusCode, PositiveInt durationMs)
    {
        return RunApiCallLogCompletionFieldsAllowedAsync(
            statusCode.Get % 600, durationMs.Get).GetAwaiter().GetResult();
    }

    private static async Task<bool> RunApiCallLogCompletionFieldsAllowedAsync(int statusCode, int durationMs)
    {
        var tenantId = Guid.NewGuid();
        var dbName = Guid.NewGuid().ToString();

        Guid logId;
        {
            var (context, _) = CreateDbContext(dbName, tenantId);
            using (context)
            {
                var apiCallLog = new ApiCallLog
                {
                    TenantId = tenantId,
                    CorrelationId = Guid.NewGuid(),
                    RequestId = Guid.NewGuid().ToString(),
                    HttpMethod = "GET",
                    Endpoint = "/api/test",
                    RequestPath = "/api/test",
                    StatusCode = 0,
                    DurationMs = 0,
                    RequestTimestamp = DateTime.UtcNow,
                    ResponseTimestamp = DateTime.UtcNow
                };
                context.ApiCallLogs.Add(apiCallLog);
                await context.SaveChangesAsync();
                logId = apiCallLog.Id;
            }
        }

        // Modify only completion fields (StatusCode, DurationMs, ResponseTimestamp) - should succeed
        {
            var (context, _) = CreateDbContext(dbName, tenantId);
            using (context)
            {
                var log = await context.ApiCallLogs
                    .IgnoreQueryFilters()
                    .FirstAsync(l => l.Id == logId);

                log.StatusCode = statusCode;
                log.DurationMs = durationMs;
                log.ResponseTimestamp = DateTime.UtcNow;

                try
                {
                    await context.SaveChangesAsync();
                    return true; // Expected: no exception
                }
                catch (InvalidOperationException)
                {
                    return false; // Should NOT have thrown for completion fields
                }
            }
        }
    }

    // ─── Controller Structural Tests (No Mutation Endpoints) ────────────────────

    /// <summary>
    /// Validates: Requirement 17.4
    /// The AuditLogsController must not expose any mutation endpoints (POST, PUT, PATCH, DELETE).
    /// Only GET (read) operations are permitted.
    /// </summary>
    [Property(MaxTest = 100)]
    public bool AuditLogsController_ExposesNoMutationEndpoints()
    {
        var controllerType = typeof(Heimdall.Api.Controllers.AuditLogsController);
        var methods = controllerType.GetMethods(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly);

        foreach (var method in methods)
        {
            var hasPost = method.GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.HttpPostAttribute), false).Any();
            var hasPut = method.GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.HttpPutAttribute), false).Any();
            var hasPatch = method.GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.HttpPatchAttribute), false).Any();
            var hasDelete = method.GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.HttpDeleteAttribute), false).Any();

            if (hasPost || hasPut || hasPatch || hasDelete)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Validates: Requirement 16.3
    /// The ApiCallLogsController must not expose any mutation endpoints (POST, PUT, PATCH, DELETE).
    /// Only GET (read) operations are permitted.
    /// </summary>
    [Property(MaxTest = 100)]
    public bool ApiCallLogsController_ExposesNoMutationEndpoints()
    {
        var controllerType = typeof(Heimdall.Api.Controllers.ApiCallLogsController);
        var methods = controllerType.GetMethods(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly);

        foreach (var method in methods)
        {
            var hasPost = method.GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.HttpPostAttribute), false).Any();
            var hasPut = method.GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.HttpPutAttribute), false).Any();
            var hasPatch = method.GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.HttpPatchAttribute), false).Any();
            var hasDelete = method.GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.HttpDeleteAttribute), false).Any();

            if (hasPost || hasPut || hasPatch || hasDelete)
                return false;
        }

        return true;
    }

    // ─── AuditLog Field Preservation After Creation ─────────────────────────────

    /// <summary>
    /// Validates: Requirement 17.4
    /// All fields of an AuditLog record are faithfully preserved after creation.
    /// Once written, the data must remain exactly as recorded.
    /// </summary>
    [Property(MaxTest = 100)]
    public bool AuditLog_FieldsArePreservedAfterCreation(
        NonEmptyString entityTypeRaw,
        NonEmptyString beforeJsonRaw,
        NonEmptyString afterJsonRaw)
    {
        var entityType = entityTypeRaw.Get[..Math.Min(entityTypeRaw.Get.Length, 50)];
        var beforeJson = beforeJsonRaw.Get[..Math.Min(beforeJsonRaw.Get.Length, 200)];
        var afterJson = afterJsonRaw.Get[..Math.Min(afterJsonRaw.Get.Length, 200)];

        return RunAuditLogFieldPreservationTestAsync(entityType, beforeJson, afterJson)
            .GetAwaiter().GetResult();
    }

    private static async Task<bool> RunAuditLogFieldPreservationTestAsync(
        string entityType, string beforeJson, string afterJson)
    {
        var tenantId = Guid.NewGuid();
        var applicationId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        var apiCallLogId = Guid.NewGuid();
        var actorUserProfileId = Guid.NewGuid();
        var entityId = Guid.NewGuid().ToString();
        var dbName = Guid.NewGuid().ToString();

        Guid auditLogId;
        {
            var (context, _) = CreateDbContext(dbName, tenantId);
            using (context)
            {
                var auditLog = new AuditLog
                {
                    TenantId = tenantId,
                    ApplicationId = applicationId,
                    CorrelationId = correlationId,
                    ApiCallLogId = apiCallLogId,
                    ActorUserProfileId = actorUserProfileId,
                    ActorSubjectId = "sub-preserve",
                    ActorEmail = "preserve@test.com",
                    EntityType = entityType,
                    EntityId = entityId,
                    Action = "Update",
                    BeforeJson = beforeJson,
                    AfterJson = afterJson,
                    ChangedFieldsJson = "[\"field1\",\"field2\"]",
                    SourceIp = "10.20.30.40",
                    UserAgent = "PreserveTest/2.0"
                };
                context.AuditLogs.Add(auditLog);
                await context.SaveChangesAsync();
                auditLogId = auditLog.Id;
            }
        }

        // Read from fresh context and verify all fields are preserved
        {
            var (context, _) = CreateDbContext(dbName, tenantId);
            using (context)
            {
                var log = await context.AuditLogs
                    .IgnoreQueryFilters()
                    .FirstAsync(a => a.Id == auditLogId);

                if (log.TenantId != tenantId) return false;
                if (log.ApplicationId != applicationId) return false;
                if (log.CorrelationId != correlationId) return false;
                if (log.ApiCallLogId != apiCallLogId) return false;
                if (log.ActorUserProfileId != actorUserProfileId) return false;
                if (log.ActorSubjectId != "sub-preserve") return false;
                if (log.ActorEmail != "preserve@test.com") return false;
                if (log.EntityType != entityType) return false;
                if (log.EntityId != entityId) return false;
                if (log.Action != "Update") return false;
                if (log.BeforeJson != beforeJson) return false;
                if (log.AfterJson != afterJson) return false;
                if (log.ChangedFieldsJson != "[\"field1\",\"field2\"]") return false;
                if (log.SourceIp != "10.20.30.40") return false;
                if (log.UserAgent != "PreserveTest/2.0") return false;
            }
        }

        return true;
    }
}
