using Heimdall.Application.PermissionTypes.Queries.GetPermissionType;
using MediatR;

namespace Heimdall.Application.PermissionTypes.Queries.GetPermissionTypes;

public sealed record GetPermissionTypesQuery(Guid ApplicationId) : IRequest<IReadOnlyList<PermissionTypeDto>>;
