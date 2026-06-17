namespace Heimdall.Domain.Entities;

public class ApiCallLog : TenantScopedEntity
{
    public Guid? ApplicationId { get; set; }
    public Guid CorrelationId { get; set; }
    public string RequestId { get; set; } = string.Empty;
    public string HttpMethod { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public string RequestPath { get; set; } = string.Empty;
    public string? CallerSubjectId { get; set; }
    public string? CallerClientId { get; set; }
    public string? SourceIp { get; set; }
    public string? UserAgent { get; set; }
    public int StatusCode { get; set; }
    public long DurationMs { get; set; }
    public DateTime RequestTimestamp { get; set; }
    public DateTime ResponseTimestamp { get; set; }
}
