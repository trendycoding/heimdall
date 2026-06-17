using MediatR;

namespace Heimdall.Application.PermissionTemplates.Commands.UpdatePermissionTemplate;

public sealed record UpdatePermissionTemplateCommand(
    Guid Id,
    string Name,
    string? Description,
    bool IsActive) : IRequest<Unit>;
