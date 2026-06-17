using Heimdall.Domain.Models;

namespace Heimdall.Domain.Interfaces;

public interface IPermissionResolver
{
    Task<PermissionCheckResult> CheckPermissionAsync(
        Guid tenantId, Guid applicationId, Guid userProfileId,
        string functionalAreaCode, string permissionTypeCode,
        DateTime? evaluationTime = null, CancellationToken ct = default);

    Task<PermissionCheckResult> CheckPermissionByCodeAsync(
        Guid tenantId, Guid applicationId, Guid userProfileId,
        string permissionCode, DateTime? evaluationTime = null,
        CancellationToken ct = default);

    Task<PermissionCheckResult> CheckPermissionByExternalIdAsync(
        Guid tenantId, Guid applicationId,
        string externalSubjectId, string identityProvider,
        string permissionCode, DateTime? evaluationTime = null,
        CancellationToken ct = default);

    Task<BatchPermissionCheckResult> CheckBatchAsync(
        Guid tenantId, Guid applicationId, Guid userProfileId,
        IReadOnlyList<PermissionCheckRequest> checks,
        DateTime? evaluationTime = null, CancellationToken ct = default);

    Task<EffectivePermissionsResult> GetEffectivePermissionsAsync(
        Guid tenantId, Guid applicationId, Guid userProfileId,
        DateTime? evaluationTime = null, CancellationToken ct = default);
}
