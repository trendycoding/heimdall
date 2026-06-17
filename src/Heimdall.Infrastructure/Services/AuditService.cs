using Heimdall.Domain.Entities;
using Heimdall.Domain.Interfaces;
using Heimdall.Domain.Models;
using Heimdall.Infrastructure.Persistence;

namespace Heimdall.Infrastructure.Services;

/// <summary>
/// Records audit log entries by adding them to the same DbContext used for entity changes.
/// The audit record is committed in the same SaveChangesAsync call as the entity mutation,
/// ensuring transactional consistency — if the audit write fails, the entire transaction rolls back.
/// </summary>
public class AuditService : IAuditService
{
    private readonly HeimdallDbContext _db;

    public AuditService(HeimdallDbContext db)
    {
        _db = db;
    }

    public async Task RecordAsync(AuditEntry entry, CancellationToken ct = default)
    {
        var auditLog = new AuditLog
        {
            TenantId = entry.TenantId,
            ApplicationId = entry.ApplicationId,
            EntityType = entry.EntityType,
            EntityId = entry.EntityId.ToString(),
            Action = entry.Action,
            ActorEmail = entry.ActorEmail,
            ActorSubjectId = entry.ActorSubjectId,
            ActorUserProfileId = entry.ActorUserProfileId,
            BeforeJson = entry.BeforeJson,
            AfterJson = entry.AfterJson,
            ChangedFieldsJson = entry.ChangedFieldsJson,
            CorrelationId = entry.CorrelationId ?? Guid.NewGuid(),
            ApiCallLogId = entry.ApiCallLogId,
            SourceIp = entry.SourceIp,
            UserAgent = entry.UserAgent
        };

        // Add to the DbContext without calling SaveChangesAsync.
        // The calling behavior/handler is responsible for committing the transaction,
        // which ensures the audit record is in the same transaction as entity changes.
        // If the commit fails (including due to audit record issues), the entire
        // transaction rolls back (Requirement 17.5).
        await _db.AuditLogs.AddAsync(auditLog, ct);
    }
}
