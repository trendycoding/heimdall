using Heimdall.Application.PermissionTemplates.Queries.GetPermissionTemplate;
using MediatR;

namespace Heimdall.Application.PermissionTemplates.Queries.GetPermissionTemplates;

public sealed record GetPermissionTemplatesQuery(Guid ApplicationId) : IRequest<IReadOnlyList<PermissionTemplateSummaryDto>>;

public sealed record PermissionTemplateSummaryDto(
    Guid Id,
    Guid TenantId,
    Guid ApplicationId,
    string TemplateCode,
    string Name,
    string? Description,
    bool IsActive,
    DateTime CreatedAt,
    string CreatedBy,
    DateTime? ModifiedAt,
    string? ModifiedBy);
