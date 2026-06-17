namespace Heimdall.Domain.Entities;

public class AuditLog : TenantScopedEntity
{
    public Guid? ApplicationId { get; set; }
    public Guid CorrelationId { get; set; }
    public Guid? ApiCallLogId { get; set; }
    public Guid? ActorUserProfileId { get; set; }
    public string? ActorSubjectId { get; set; }
    public string? ActorEmail { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
    public string? ChangedFieldsJson { get; set; }
    public string? SourceIp { get; set; }
    public string? UserAgent { get; set; }
}
