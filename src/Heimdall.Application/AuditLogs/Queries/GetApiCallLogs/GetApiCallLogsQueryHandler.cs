using Heimdall.Application.Common.Interfaces;
using Heimdall.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Heimdall.Application.AuditLogs.Queries.GetApiCallLogs;

public sealed class GetApiCallLogsQueryHandler : IRequestHandler<GetApiCallLogsQuery, PaginatedResult<ApiCallLogDto>>
{
    private readonly IHeimdallDbContext _dbContext;

    public GetApiCallLogsQueryHandler(IHeimdallDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PaginatedResult<ApiCallLogDto>> Handle(GetApiCallLogsQuery request, CancellationToken cancellationToken)
    {
        var pageSize = Math.Min(request.PageSize, 100);
        var page = Math.Max(request.Page, 1);

        var query = _dbContext.ApiCallLogs.AsQueryable();

        // Apply filters as AND conditions
        query = query.Where(a => a.TenantId == request.TenantId);

        if (request.ApplicationId.HasValue)
            query = query.Where(a => a.ApplicationId == request.ApplicationId.Value);

        if (request.CorrelationId.HasValue)
            query = query.Where(a => a.CorrelationId == request.CorrelationId.Value);

        if (!string.IsNullOrWhiteSpace(request.Endpoint))
            query = query.Where(a => a.Endpoint == request.Endpoint);

        if (!string.IsNullOrWhiteSpace(request.HttpMethod))
            query = query.Where(a => a.HttpMethod == request.HttpMethod);

        if (!string.IsNullOrWhiteSpace(request.SourceIp))
            query = query.Where(a => a.SourceIp == request.SourceIp);

        if (!string.IsNullOrWhiteSpace(request.CallerSubjectId))
            query = query.Where(a => a.CallerSubjectId == request.CallerSubjectId);

        if (request.DateFrom.HasValue)
            query = query.Where(a => a.CreatedAt >= request.DateFrom.Value);

        if (request.DateTo.HasValue)
            query = query.Where(a => a.CreatedAt <= request.DateTo.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new ApiCallLogDto(
                a.Id,
                a.TenantId,
                a.ApplicationId,
                a.CorrelationId,
                a.RequestId,
                a.HttpMethod,
                a.Endpoint,
                a.RequestPath,
                a.CallerSubjectId,
                a.CallerClientId,
                a.SourceIp,
                a.UserAgent,
                a.StatusCode,
                a.DurationMs,
                a.RequestTimestamp,
                a.ResponseTimestamp,
                a.CreatedAt,
                a.CreatedBy))
            .ToListAsync(cancellationToken);

        return new PaginatedResult<ApiCallLogDto>(items, totalCount, page, pageSize);
    }
}
