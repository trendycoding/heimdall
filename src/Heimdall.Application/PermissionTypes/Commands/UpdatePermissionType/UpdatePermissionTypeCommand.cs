using MediatR;

namespace Heimdall.Application.PermissionTypes.Commands.UpdatePermissionType;

public sealed record UpdatePermissionTypeCommand(
    Guid Id,
    string Code,
    string Name,
    string? Description) : IRequest<Unit>;
