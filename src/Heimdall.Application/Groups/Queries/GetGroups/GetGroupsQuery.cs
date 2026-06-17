using Heimdall.Application.Groups.Queries.GetGroup;
using MediatR;

namespace Heimdall.Application.Groups.Queries.GetGroups;

public sealed record GetGroupsQuery(Guid ApplicationId) : IRequest<IReadOnlyList<GroupDto>>;
