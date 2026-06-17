using MediatR;

namespace Heimdall.Application.PermissionTemplates.Commands.CreatePermissionTemplate;

public sealed record CreatePermissionTemplateCommand(
    Guid ApplicationId,
    string TemplateCode,
    string Name,
    string? Description) : IRequest<CreatePermissionTemplateResult>;

public sealed record CreatePermissionTemplateResult(Guid PermissionTemplateId);
