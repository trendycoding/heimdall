using Heimdall.Domain.Models;

namespace Heimdall.Domain.Interfaces;

public interface IAuditService
{
    Task RecordAsync(AuditEntry entry, CancellationToken ct = default);
}
