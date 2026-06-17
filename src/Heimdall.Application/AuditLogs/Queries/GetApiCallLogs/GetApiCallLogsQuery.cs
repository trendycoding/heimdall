using Heimdall.Application.Common.Models;
using MediatR;

namespace Heimdall.Application.AuditLogs.Queries.GetApiCallLogs;

public sealed record GetApiCallLogsQuery : IRequest<PaginatedResult<ApiCallLogDto>>
{
    public required Guid TenantId { get; init; }
    public Guid? ApplicationId { get; init; }
    public Guid? CorrelationId { get; init; }
    public string? Endpoint { get; init; }
    public string? HttpMethod { get; init; }
    public string? SourceIp { get; init; }
    public string? CallerSubjectId { get; init; }
    public DateTime? DateFrom { get; init; }
    public DateTime? DateTo { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}

public sealed record ApiCallLogDto(
    Guid Id,
    Guid TenantId,
    Guid? ApplicationId,
    Guid CorrelationId,
    string RequestId,
    string HttpMethod,
    string Endpoint,
    string RequestPath,
    string? CallerSubjectId,
    string? CallerClientId,
    string? SourceIp,
    string? UserAgent,
    int StatusCode,
    long DurationMs,
    DateTime RequestTimestamp,
    DateTime ResponseTimestamp,
    DateTime CreatedAt,
    string CreatedBy);
