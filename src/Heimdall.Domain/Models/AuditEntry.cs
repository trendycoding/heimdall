namespace Heimdall.Domain.Models;

public sealed class AuditEntry
{
    public Guid TenantId { get; init; }
    public Guid? ApplicationId { get; init; }
    public string EntityType { get; init; } = string.Empty;
    public Guid EntityId { get; init; }
    public string Action { get; init; } = string.Empty;
    public string? BeforeJson { get; init; }
    public string? AfterJson { get; init; }
    public string? ChangedFieldsJson { get; init; }
    public string ActorSubjectId { get; init; } = string.Empty;
    public string ActorEmail { get; init; } = string.Empty;
    public Guid? ActorUserProfileId { get; init; }
    public Guid? CorrelationId { get; init; }
    public Guid? ApiCallLogId { get; init; }
    public string? SourceIp { get; init; }
    public string? UserAgent { get; init; }
}
