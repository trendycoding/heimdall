using MediatR;

namespace Heimdall.Application.PermissionTemplates.Commands.AddPermissionTemplateAccessDetail;

public sealed record AddPermissionTemplateAccessDetailCommand(
    Guid PermissionTemplateId,
    string AccessDetailType,
    string AccessDetailCode,
    string AccessDetailValue,
    string? Description,
    int? ValidFromOffsetDays,
    int? ValidToOffsetDays) : IRequest<AddPermissionTemplateAccessDetailResult>;

public sealed record AddPermissionTemplateAccessDetailResult(Guid Id);
