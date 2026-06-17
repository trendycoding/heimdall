using MediatR;

namespace Heimdall.Application.PermissionTemplates.Commands.DeactivatePermissionTemplate;

public sealed record DeactivatePermissionTemplateCommand(Guid Id) : IRequest<Unit>;
