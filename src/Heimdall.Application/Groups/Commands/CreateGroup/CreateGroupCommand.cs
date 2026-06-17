using MediatR;

namespace Heimdall.Application.Groups.Commands.CreateGroup;

public sealed record CreateGroupCommand(
    Guid ApplicationId,
    string Name,
    string? Description) : IRequest<CreateGroupResult>;

public sealed record CreateGroupResult(Guid Id);
