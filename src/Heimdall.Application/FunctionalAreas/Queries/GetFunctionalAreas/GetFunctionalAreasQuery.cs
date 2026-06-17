using Heimdall.Application.FunctionalAreas.Queries.GetFunctionalArea;
using MediatR;

namespace Heimdall.Application.FunctionalAreas.Queries.GetFunctionalAreas;

public sealed record GetFunctionalAreasQuery(Guid ApplicationId) : IRequest<IReadOnlyList<FunctionalAreaDto>>;
