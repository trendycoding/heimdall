using MediatR;

namespace Heimdall.Application.FunctionalAreas.Commands.CreateFunctionalArea;

public sealed record CreateFunctionalAreaCommand(
    Guid ApplicationId,
    string FunctionalAreaCode,
    string Name,
    string? Description) : IRequest<CreateFunctionalAreaResult>;

public sealed record CreateFunctionalAreaResult(Guid Id);
