using MediatR;

namespace Heimdall.Application.PermissionTypes.Commands.CreatePermissionType;

public sealed record CreatePermissionTypeCommand(
    Guid ApplicationId,
    string Code,
    string Name,
    string? Description,
    bool IsSystemReserved = false) : IRequest<CreatePermissionTypeResult>;

public sealed record CreatePermissionTypeResult(Guid Id);
