using Heimdall.Domain.Models;

namespace Heimdall.Domain.Interfaces;

public interface ITemplateApplicationService
{
    Task<TemplateApplicationResult> ApplyTemplateAsync(
        Guid tenantId, Guid applicationId, Guid userProfileId,
        Guid permissionTemplateId, TemplateApplicationOptions options,
        CancellationToken ct = default);

    Task<TemplatePreviewResult> PreviewTemplateAsync(
        Guid tenantId, Guid applicationId, Guid userProfileId,
        Guid permissionTemplateId, TemplateApplicationOptions options,
        CancellationToken ct = default);
}
