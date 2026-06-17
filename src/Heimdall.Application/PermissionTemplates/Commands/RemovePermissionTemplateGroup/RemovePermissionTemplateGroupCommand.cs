using MediatR;

namespace Heimdall.Application.PermissionTemplates.Commands.RemovePermissionTemplateGroup;

public sealed record RemovePermissionTemplateGroupCommand(Guid Id) : IRequest<Unit>;
