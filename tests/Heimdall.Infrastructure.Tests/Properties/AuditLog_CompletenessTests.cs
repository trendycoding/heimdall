using FsCheck;
using FsCheck.Xunit;
using Heimdall.Domain.Entities;
using Heimdall.Domain.Interfaces;
using Heimdall.Domain.Models;
using Heimdall.Infrastructure.Persistence;
using Heimdall.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;

namespace Heimdall.Infrastructure.Tests.Properties;

/// <summary>
/// Property 20: Audit Log Completeness
/// Generate state-changing operations; assert AuditLog records are created with correct
/// BeforeJson, AfterJson, ApiCallLogId, CorrelationId, and actor metadata.
///
/// **Validates: Requirements 17.1, 17.2, 17.3**
/// </summary>
public class AuditLog_CompletenessTests
{
    private static HeimdallDbContext CreateDbContext(ITenantContext tenantContext)
    {
        var options = new DbContextOptionsBuilder<HeimdallDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new HeimdallDbContext(options, tenantContext);
    }

    [Property(MaxTest = 100)]
    public bool CreateAction_HasNullBeforeJson_AndFullAfterJson(
        NonEmptyString entityTypeRaw,
        NonEmptyString afterJsonRaw)
    {
        return RunCreateAuditTestAsync(
            entityTypeRaw.Get[..Math.Min(entityTypeRaw.Get.Length, 50)],
            afterJsonRaw.Get[..Math.Min(afterJsonRaw.Get.Length, 200)]
        ).GetAwaiter().GetResult();
    }

    [Property(MaxTest = 100)]
    public bool AuditRecord_PreservesCorrelationId(
        Guid correlationId,
        NonEmptyString entityTypeRaw)
    {
        var entityType = entityTypeRaw.Get[..Math.Min(entityTypeRaw.Get.Length, 50)];
        return RunCorrelationIdTestAsync(correlationId, entityType).GetAwaiter().GetResult();
    }

    [Property(MaxTest = 100)]
    public bool AuditRecord_PreservesApiCallLogId(
        Guid apiCallLogId,
        NonEmptyString entityTypeRaw)
    {
        var entityType = entityTypeRaw.Get[..Math.Min(entityTypeRaw.Get.Length, 50)];
        return RunApiCallLogIdTestAsync(apiCallLogId, entityType).GetAwaiter().GetResult();
    }

    [Property(MaxTest = 100)]
    public bool AuditRecord_PreservesActorMetadata(
        NonEmptyString actorSubjectIdRaw,
        NonEmptyString actorEmailRaw)
    {
        var actorSubjectId = actorSubjectIdRaw.Get[..Math.Min(actorSubjectIdRaw.Get.Length, 100)];
        var actorEmail = actorEmailRaw.Get[..Math.Min(actorEmailRaw.Get.Length, 100)];
        return RunActorMetadataTestAsync(actorSubjectId, actorEmail).GetAwaiter().GetResult();
    }

    [Property(MaxTest = 100)]
    public bool DeleteAction_HasFullBeforeJson_AndNullAfterJson(
        NonEmptyString entityTypeRaw,
        NonEmptyString beforeJsonRaw)
    {
        return RunDeleteAuditTestAsync(
            entityTypeRaw.Get[..Math.Min(entityTypeRaw.Get.Length, 50)],
            beforeJsonRaw.Get[..Math.Min(beforeJsonRaw.Get.Length, 200)]
        ).GetAwaiter().GetResult();
    }

    [Property(MaxTest = 100)]
    public bool UpdateAction_HasBothBeforeAndAfterJson(
        NonEmptyString entityTypeRaw,
        NonEmptyString beforeJsonRaw,
        NonEmptyString afterJsonRaw)
    {
        return RunUpdateAuditTestAsync(
            entityTypeRaw.Get[..Math.Min(entityTypeRaw.Get.Length, 50)],
            beforeJsonRaw.Get[..Math.Min(beforeJsonRaw.Get.Length, 200)],
            afterJsonRaw.Get[..Math.Min(afterJsonRaw.Get.Length, 200)]
        ).GetAwaiter().GetResult();
    }

    [Property(MaxTest = 100)]
    public bool SharedCorrelationId_LinksMutipleAuditRecords(
        PositiveInt recordCountRaw)
    {
        var recordCount = (recordCountRaw.Get % 5) + 2; // 2-6 records sharing same correlation
        return RunSharedCorrelationTestAsync(recordCount).GetAwaiter().GetResult();
    }

    private static async Task<bool> RunCreateAuditTestAsync(string entityType, string afterJson)
    {
        var tenantId = Guid.NewGuid();
        var applicationId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        var apiCallLogId = Guid.NewGuid();
        var actorUserProfileId = Guid.NewGuid();
        var entityId = Guid.NewGuid();

        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(tenantId);
        tenantContext.ActorEmail.Returns("auditor@test.com");

        using var context = CreateDbContext(tenantContext);
        var auditService = new AuditService(context);

        var entry = new AuditEntry
        {
            TenantId = tenantId,
            ApplicationId = applicationId,
            EntityType = entityType,
            EntityId = entityId,
            Action = "Create",
            BeforeJson = null,
            AfterJson = afterJson,
            ChangedFieldsJson = null,
            CorrelationId = correlationId,
            ApiCallLogId = apiCallLogId,
            ActorUserProfileId = actorUserProfileId,
            ActorSubjectId = "sub-create-test",
            ActorEmail = "creator@test.com",
            SourceIp = "10.0.0.1",
            UserAgent = "TestAgent/1.0"
        };

        await auditService.RecordAsync(entry);
        await context.SaveChangesAsync();

        // Verify
        var auditLog = await context.AuditLogs
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.EntityId == entityId.ToString());

        if (auditLog == null) return false;

        // Requirement 17.2: Create → BeforeJson is null, AfterJson is full state
        if (auditLog.BeforeJson != null) return false;
        if (auditLog.AfterJson != afterJson) return false;

        return true;
    }

    private static async Task<bool> RunCorrelationIdTestAsync(Guid correlationId, string entityType)
    {
        var tenantId = Guid.NewGuid();
        var entityId = Guid.NewGuid();

        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(tenantId);
        tenantContext.ActorEmail.Returns("auditor@test.com");

        using var context = CreateDbContext(tenantContext);
        var auditService = new AuditService(context);

        var entry = new AuditEntry
        {
            TenantId = tenantId,
            ApplicationId = null,
            EntityType = entityType,
            EntityId = entityId,
            Action = "Create",
            BeforeJson = null,
            AfterJson = "{\"id\": \"test\"}",
            CorrelationId = correlationId,
            ApiCallLogId = null,
            ActorSubjectId = "sub-corr-test",
            ActorEmail = "corr@test.com"
        };

        await auditService.RecordAsync(entry);
        await context.SaveChangesAsync();

        var auditLog = await context.AuditLogs
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.EntityId == entityId.ToString());

        if (auditLog == null) return false;

        // Requirement 17.3: CorrelationId must be preserved
        return auditLog.CorrelationId == correlationId;
    }

    private static async Task<bool> RunApiCallLogIdTestAsync(Guid apiCallLogId, string entityType)
    {
        var tenantId = Guid.NewGuid();
        var entityId = Guid.NewGuid();

        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(tenantId);
        tenantContext.ActorEmail.Returns("auditor@test.com");

        using var context = CreateDbContext(tenantContext);
        var auditService = new AuditService(context);

        var entry = new AuditEntry
        {
            TenantId = tenantId,
            ApplicationId = Guid.NewGuid(),
            EntityType = entityType,
            EntityId = entityId,
            Action = "Update",
            BeforeJson = "{\"old\": true}",
            AfterJson = "{\"new\": true}",
            CorrelationId = Guid.NewGuid(),
            ApiCallLogId = apiCallLogId,
            ActorSubjectId = "sub-api-test",
            ActorEmail = "api@test.com"
        };

        await auditService.RecordAsync(entry);
        await context.SaveChangesAsync();

        var auditLog = await context.AuditLogs
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.EntityId == entityId.ToString());

        if (auditLog == null) return false;

        // Requirement 17.3: ApiCallLogId must be preserved
        return auditLog.ApiCallLogId == apiCallLogId;
    }

    private static async Task<bool> RunActorMetadataTestAsync(string actorSubjectId, string actorEmail)
    {
        var tenantId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        var actorUserProfileId = Guid.NewGuid();

        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(tenantId);
        tenantContext.ActorEmail.Returns("auditor@test.com");

        using var context = CreateDbContext(tenantContext);
        var auditService = new AuditService(context);

        var entry = new AuditEntry
        {
            TenantId = tenantId,
            ApplicationId = Guid.NewGuid(),
            EntityType = "UserProfile",
            EntityId = entityId,
            Action = "Create",
            BeforeJson = null,
            AfterJson = "{\"status\": \"active\"}",
            CorrelationId = Guid.NewGuid(),
            ApiCallLogId = Guid.NewGuid(),
            ActorSubjectId = actorSubjectId,
            ActorEmail = actorEmail,
            ActorUserProfileId = actorUserProfileId,
            SourceIp = "192.168.1.1",
            UserAgent = "PropTest/1.0"
        };

        await auditService.RecordAsync(entry);
        await context.SaveChangesAsync();

        var auditLog = await context.AuditLogs
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.EntityId == entityId.ToString());

        if (auditLog == null) return false;

        // Requirement 17.1: Actor metadata must be preserved
        if (auditLog.ActorSubjectId != actorSubjectId) return false;
        if (auditLog.ActorEmail != actorEmail) return false;
        if (auditLog.ActorUserProfileId != actorUserProfileId) return false;
        if (auditLog.SourceIp != "192.168.1.1") return false;
        if (auditLog.UserAgent != "PropTest/1.0") return false;

        return true;
    }

    private static async Task<bool> RunDeleteAuditTestAsync(string entityType, string beforeJson)
    {
        var tenantId = Guid.NewGuid();
        var entityId = Guid.NewGuid();

        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(tenantId);
        tenantContext.ActorEmail.Returns("auditor@test.com");

        using var context = CreateDbContext(tenantContext);
        var auditService = new AuditService(context);

        var entry = new AuditEntry
        {
            TenantId = tenantId,
            ApplicationId = Guid.NewGuid(),
            EntityType = entityType,
            EntityId = entityId,
            Action = "Delete",
            BeforeJson = beforeJson,
            AfterJson = null,
            CorrelationId = Guid.NewGuid(),
            ApiCallLogId = Guid.NewGuid(),
            ActorSubjectId = "sub-delete-test",
            ActorEmail = "deleter@test.com"
        };

        await auditService.RecordAsync(entry);
        await context.SaveChangesAsync();

        var auditLog = await context.AuditLogs
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.EntityId == entityId.ToString());

        if (auditLog == null) return false;

        // Requirement 17.2: Delete → BeforeJson is full state, AfterJson is null
        if (auditLog.BeforeJson != beforeJson) return false;
        if (auditLog.AfterJson != null) return false;

        return true;
    }

    private static async Task<bool> RunUpdateAuditTestAsync(
        string entityType, string beforeJson, string afterJson)
    {
        var tenantId = Guid.NewGuid();
        var entityId = Guid.NewGuid();

        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(tenantId);
        tenantContext.ActorEmail.Returns("auditor@test.com");

        using var context = CreateDbContext(tenantContext);
        var auditService = new AuditService(context);

        var entry = new AuditEntry
        {
            TenantId = tenantId,
            ApplicationId = Guid.NewGuid(),
            EntityType = entityType,
            EntityId = entityId,
            Action = "Update",
            BeforeJson = beforeJson,
            AfterJson = afterJson,
            ChangedFieldsJson = "[\"Name\"]",
            CorrelationId = Guid.NewGuid(),
            ApiCallLogId = Guid.NewGuid(),
            ActorSubjectId = "sub-update-test",
            ActorEmail = "updater@test.com"
        };

        await auditService.RecordAsync(entry);
        await context.SaveChangesAsync();

        var auditLog = await context.AuditLogs
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.EntityId == entityId.ToString());

        if (auditLog == null) return false;

        // Requirement 17.1, 17.2: Update → both BeforeJson and AfterJson preserved
        if (auditLog.BeforeJson != beforeJson) return false;
        if (auditLog.AfterJson != afterJson) return false;
        if (auditLog.ChangedFieldsJson != "[\"Name\"]") return false;

        return true;
    }

    private static async Task<bool> RunSharedCorrelationTestAsync(int recordCount)
    {
        var tenantId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        var apiCallLogId = Guid.NewGuid();

        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(tenantId);
        tenantContext.ActorEmail.Returns("auditor@test.com");

        using var context = CreateDbContext(tenantContext);
        var auditService = new AuditService(context);

        // Simulate a single API call that produces multiple audit records
        // (e.g., template application affecting multiple entities)
        for (int i = 0; i < recordCount; i++)
        {
            var entry = new AuditEntry
            {
                TenantId = tenantId,
                ApplicationId = Guid.NewGuid(),
                EntityType = $"Entity_{i}",
                EntityId = Guid.NewGuid(),
                Action = "Create",
                BeforeJson = null,
                AfterJson = $"{{\"index\": {i}}}",
                CorrelationId = correlationId,
                ApiCallLogId = apiCallLogId,
                ActorSubjectId = "sub-batch-test",
                ActorEmail = "batch@test.com"
            };

            await auditService.RecordAsync(entry);
        }

        await context.SaveChangesAsync();

        // Requirement 17.3: All records from same API call share CorrelationId and ApiCallLogId
        var auditLogs = await context.AuditLogs
            .IgnoreQueryFilters()
            .Where(a => a.CorrelationId == correlationId)
            .ToListAsync();

        if (auditLogs.Count != recordCount) return false;

        // All records must share the same ApiCallLogId
        return auditLogs.All(a => a.ApiCallLogId == apiCallLogId);
    }
}
