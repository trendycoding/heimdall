using Heimdall.Domain.Enums;
using MediatR;

namespace Heimdall.Application.PermissionTemplates.Commands.AddPermissionTemplatePermission;

public sealed record AddPermissionTemplatePermissionCommand(
    Guid PermissionTemplateId,
    Guid PermissionId,
    Effect Effect,
    int? ValidFromOffsetDays,
    int? ValidToOffsetDays) : IRequest<AddPermissionTemplatePermissionResult>;

public sealed record AddPermissionTemplatePermissionResult(Guid Id);
