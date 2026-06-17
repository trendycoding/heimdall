using Heimdall.Application.Common.Interfaces;
using Heimdall.Application.PermissionTypes.Queries.GetPermissionType;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Heimdall.Application.PermissionTypes.Queries.GetPermissionTypes;

public sealed class GetPermissionTypesQueryHandler : IRequestHandler<GetPermissionTypesQuery, IReadOnlyList<PermissionTypeDto>>
{
    private readonly IHeimdallDbContext _dbContext;

    public GetPermissionTypesQueryHandler(IHeimdallDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<PermissionTypeDto>> Handle(GetPermissionTypesQuery request, CancellationToken cancellationToken)
    {
        var entities = await _dbContext.PermissionTypes
            .Where(pt => pt.ApplicationId == request.ApplicationId)
            .OrderBy(pt => pt.Code)
            .ToListAsync(cancellationToken);

        return entities.Select(e => new PermissionTypeDto(
            e.Id,
            e.TenantId,
            e.ApplicationId,
            e.Code,
            e.Name,
            e.Description,
            e.IsSystemReserved,
            e.IsActive,
            e.CreatedAt,
            e.CreatedBy,
            e.ModifiedAt,
            e.ModifiedBy)).ToList();
    }
}
