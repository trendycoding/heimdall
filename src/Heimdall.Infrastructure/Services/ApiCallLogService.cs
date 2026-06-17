using System.Diagnostics;
using Heimdall.Domain.Entities;
using Heimdall.Domain.Interfaces;
using Heimdall.Domain.Models;
using Heimdall.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Heimdall.Infrastructure.Services;

/// <summary>
/// Persists API call log entries to track request/response lifecycle.
/// Sensitive data (request bodies, raw tokens) is never stored.
/// If logging fails, a diagnostic warning is emitted but the request is not blocked.
/// </summary>
public class ApiCallLogService : IApiCallLogService
{
    private readonly HeimdallDbContext _db;
    private readonly ILogger<ApiCallLogService> _logger;

    public ApiCallLogService(HeimdallDbContext db, ILogger<ApiCallLogService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<Guid> BeginLogAsync(ApiCallLogEntry entry, CancellationToken ct = default)
    {
        try
        {
            var log = new ApiCallLog
            {
                TenantId = entry.TenantId,
                ApplicationId = entry.ApplicationId,
                CorrelationId = entry.CorrelationId ?? Guid.NewGuid(),
                RequestId = Activity.Current?.Id ?? Guid.NewGuid().ToString(),
                HttpMethod = entry.HttpMethod,
                Endpoint = entry.Endpoint ?? entry.RequestPath,
                RequestPath = entry.RequestPath,
                CallerSubjectId = entry.ActorSubjectId,
                CallerClientId = entry.CallerClientId,
                SourceIp = entry.ClientIpAddress,
                UserAgent = entry.UserAgent,
                RequestTimestamp = entry.RequestedAt,
                ResponseTimestamp = entry.RequestedAt, // Will be updated on complete
                StatusCode = 0,
                DurationMs = 0
            };

            await _db.ApiCallLogs.AddAsync(log, ct);
            await _db.SaveChangesAsync(ct);

            return log.Id;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist API call log entry. Request processing will continue.");
            throw; // Re-throw so the middleware's try/catch can handle the fallback
        }
    }

    public async Task CompleteLogAsync(Guid apiCallLogId, int statusCode, long durationMs, CancellationToken ct = default)
    {
        try
        {
            var log = await _db.ApiCallLogs
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(l => l.Id == apiCallLogId, ct);

            if (log is null)
            {
                _logger.LogWarning("API call log with ID {ApiCallLogId} not found for completion.", apiCallLogId);
                return;
            }

            log.StatusCode = statusCode;
            log.DurationMs = durationMs;
            log.ResponseTimestamp = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to complete API call log {ApiCallLogId}. Request processing will continue.", apiCallLogId);
            throw; // Re-throw so the middleware's try/catch can handle the fallback
        }
    }
}
