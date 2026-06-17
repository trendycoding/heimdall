namespace Heimdall.Domain.Entities;

public class UserPermissionTemplateApplication : TenantScopedEntity
{
    public Guid ApplicationId { get; set; }
    public Guid UserProfileId { get; set; }
    public Guid PermissionTemplateId { get; set; }
    public DateTime AppliedAt { get; set; }
    public string AppliedBy { get; set; } = string.Empty;
    public string? SourceIp { get; set; }
    public string? UserAgent { get; set; }
    public Guid CorrelationId { get; set; }
    public Guid? ApiCallLogId { get; set; }
}
