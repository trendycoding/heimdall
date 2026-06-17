using MediatR;

namespace Heimdall.Application.PermissionTemplates.Commands.RemovePermissionTemplatePermission;

public sealed record RemovePermissionTemplatePermissionCommand(Guid Id) : IRequest<Unit>;
