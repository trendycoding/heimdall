using Heimdall.Domain.Models;

namespace Heimdall.Domain.Interfaces;

public interface IAccessDetailResolver
{
    Task<AccessDetailLookupResult> GetAccessDetailsAsync(
        Guid tenantId, Guid applicationId, Guid userProfileId,
        string functionalAreaCode, DateTime? evaluationTime = null,
        CancellationToken ct = default);
}
