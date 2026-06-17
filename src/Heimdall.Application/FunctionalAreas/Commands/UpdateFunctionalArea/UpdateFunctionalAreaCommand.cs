using MediatR;

namespace Heimdall.Application.FunctionalAreas.Commands.UpdateFunctionalArea;

public sealed record UpdateFunctionalAreaCommand(
    Guid Id,
    string Name,
    string? Description) : IRequest<Unit>;
