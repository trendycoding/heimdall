using MediatR;

namespace Heimdall.Application.PermissionTemplates.Commands.AddPermissionTemplateGroup;

public sealed record AddPermissionTemplateGroupCommand(
    Guid PermissionTemplateId,
    Guid GroupId) : IRequest<AddPermissionTemplateGroupResult>;

public sealed record AddPermissionTemplateGroupResult(Guid Id);
