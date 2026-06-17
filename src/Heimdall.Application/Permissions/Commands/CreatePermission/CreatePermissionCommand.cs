using MediatR;

namespace Heimdall.Application.Permissions.Commands.CreatePermission;

public sealed record CreatePermissionCommand(
    Guid ApplicationId,
    Guid FunctionalAreaId,
    Guid PermissionTypeId,
    string PermissionCode,
    string Name,
    string? Description) : IRequest<CreatePermissionResult>;

public sealed record CreatePermissionResult(Guid Id);
