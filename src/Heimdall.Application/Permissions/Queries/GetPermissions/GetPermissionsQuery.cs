using Heimdall.Application.Permissions.Queries.GetPermission;
using MediatR;

namespace Heimdall.Application.Permissions.Queries.GetPermissions;

public sealed record GetPermissionsQuery(Guid ApplicationId) : IRequest<IReadOnlyList<PermissionDto>>;
