using Heimdall.Application.Common.Interfaces;
using Heimdall.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Heimdall.Application.AuditLogs.Queries.GetAuditLogs;

public sealed class GetAuditLogsQueryHandler : IRequestHandler<GetAuditLogsQuery, PaginatedResult<AuditLogDto>>
{
    private readonly IHeimdallDbContext _dbContext;

    public GetAuditLogsQueryHandler(IHeimdallDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PaginatedResult<AuditLogDto>> Handle(GetAuditLogsQuery request, CancellationToken cancellationToken)
    {
        var pageSize = Math.Min(request.PageSize, 100);
        var page = Math.Max(request.Page, 1);

        var query = _dbContext.AuditLogs.AsQueryable();

        // Apply filters as AND conditions
        query = query.Where(a => a.TenantId == request.TenantId);

        if (request.ApplicationId.HasValue)
            query = query.Where(a => a.ApplicationId == request.ApplicationId.Value);

        if (!string.IsNullOrWhiteSpace(request.EntityType))
            query = query.Where(a => a.EntityType == request.EntityType);

        if (!string.IsNullOrWhiteSpace(request.EntityId))
            query = query.Where(a => a.EntityId == request.EntityId);

        if (!string.IsNullOrWhiteSpace(request.Action))
            query = query.Where(a => a.Action == request.Action);

        if (request.ActorUserProfileId.HasValue)
            query = query.Where(a => a.ActorUserProfileId == request.ActorUserProfileId.Value);

        if (request.DateFrom.HasValue)
            query = query.Where(a => a.CreatedAt >= request.DateFrom.Value);

        if (request.DateTo.HasValue)
            query = query.Where(a => a.CreatedAt <= request.DateTo.Value);

        if (request.CorrelationId.HasValue)
            query = query.Where(a => a.CorrelationId == request.CorrelationId.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AuditLogDto(
                a.Id,
                a.TenantId,
                a.ApplicationId,
                a.CorrelationId,
                a.ApiCallLogId,
                a.ActorUserProfileId,
                a.ActorSubjectId,
                a.ActorEmail,
                a.EntityType,
                a.EntityId,
                a.Action,
                a.BeforeJson,
                a.AfterJson,
                a.ChangedFieldsJson,
                a.SourceIp,
                a.UserAgent,
                a.CreatedAt,
                a.CreatedBy))
            .ToListAsync(cancellationToken);

        return new PaginatedResult<AuditLogDto>(items, totalCount, page, pageSize);
    }
}
