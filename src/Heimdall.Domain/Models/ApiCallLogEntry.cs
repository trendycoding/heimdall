namespace Heimdall.Domain.Models;

public sealed class ApiCallLogEntry
{
    public Guid TenantId { get; init; }
    public Guid? ApplicationId { get; init; }
    public string HttpMethod { get; init; } = string.Empty;
    public string RequestPath { get; init; } = string.Empty;
    public string? Endpoint { get; init; }
    public string? QueryString { get; init; }
    public string? ClientIpAddress { get; init; }
    public string? UserAgent { get; init; }
    public string? ActorSubjectId { get; init; }
    public string? CallerClientId { get; init; }
    public Guid? CorrelationId { get; init; }
    public DateTime RequestedAt { get; init; }
}
