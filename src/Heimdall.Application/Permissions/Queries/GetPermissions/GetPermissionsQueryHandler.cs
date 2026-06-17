using Heimdall.Application.Common.Interfaces;
using Heimdall.Application.Permissions.Queries.GetPermission;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Heimdall.Application.Permissions.Queries.GetPermissions;

public sealed class GetPermissionsQueryHandler : IRequestHandler<GetPermissionsQuery, IReadOnlyList<PermissionDto>>
{
    private readonly IHeimdallDbContext _dbContext;

    public GetPermissionsQueryHandler(IHeimdallDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<PermissionDto>> Handle(GetPermissionsQuery request, CancellationToken cancellationToken)
    {
        var entities = await _dbContext.Permissions
            .Where(p => p.ApplicationId == request.ApplicationId)
            .OrderBy(p => p.PermissionCode)
            .ToListAsync(cancellationToken);

        return entities.Select(e => new PermissionDto(
            e.Id,
            e.TenantId,
            e.ApplicationId,
            e.FunctionalAreaId,
            e.PermissionTypeId,
            e.PermissionCode,
            e.Name,
            e.Description,
            e.IsActive,
            e.CreatedAt,
            e.CreatedBy,
            e.ModifiedAt,
            e.ModifiedBy)).ToList();
    }
}
