using Heimdall.Domain.Models;

namespace Heimdall.Domain.Interfaces;

public interface IApiCallLogService
{
    Task<Guid> BeginLogAsync(ApiCallLogEntry entry, CancellationToken ct = default);
    Task CompleteLogAsync(Guid apiCallLogId, int statusCode, long durationMs, CancellationToken ct = default);
}
