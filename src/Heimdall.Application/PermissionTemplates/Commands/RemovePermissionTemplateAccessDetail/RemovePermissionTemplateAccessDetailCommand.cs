using MediatR;

namespace Heimdall.Application.PermissionTemplates.Commands.RemovePermissionTemplateAccessDetail;

public sealed record RemovePermissionTemplateAccessDetailCommand(Guid Id) : IRequest<Unit>;
