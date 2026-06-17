using MediatR;

namespace Heimdall.Application.Permissions.Commands.UpdatePermission;

public sealed record UpdatePermissionCommand(
    Guid Id,
    string PermissionCode,
    string Name,
    string? Description) : IRequest<Unit>;
