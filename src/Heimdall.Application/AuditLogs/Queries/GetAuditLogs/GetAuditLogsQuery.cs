using Heimdall.Application.Common.Models;
using MediatR;

namespace Heimdall.Application.AuditLogs.Queries.GetAuditLogs;

public sealed record GetAuditLogsQuery : IRequest<PaginatedResult<AuditLogDto>>
{
    public required Guid TenantId { get; init; }
    public Guid? ApplicationId { get; init; }
    public string? EntityType { get; init; }
    public string? EntityId { get; init; }
    public string? Action { get; init; }
    public Guid? ActorUserProfileId { get; init; }
    public DateTime? DateFrom { get; init; }
    public DateTime? DateTo { get; init; }
    public Guid? CorrelationId { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}

public sealed record AuditLogDto(
    Guid Id,
    Guid TenantId,
    Guid? ApplicationId,
    Guid CorrelationId,
    Guid? ApiCallLogId,
    Guid? ActorUserProfileId,
    string? ActorSubjectId,
    string? ActorEmail,
    string EntityType,
    string EntityId,
    string Action,
    string? BeforeJson,
    string? AfterJson,
    string? ChangedFieldsJson,
    string? SourceIp,
    string? UserAgent,
    DateTime CreatedAt,
    string CreatedBy);
