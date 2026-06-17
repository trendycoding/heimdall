using Heimdall.Application.Common.Interfaces;
using Heimdall.Application.Groups.Queries.GetGroup;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Heimdall.Application.Groups.Queries.GetGroups;

public sealed class GetGroupsQueryHandler : IRequestHandler<GetGroupsQuery, IReadOnlyList<GroupDto>>
{
    private readonly IHeimdallDbContext _dbContext;

    public GetGroupsQueryHandler(IHeimdallDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<GroupDto>> Handle(GetGroupsQuery request, CancellationToken cancellationToken)
    {
        var entities = await _dbContext.Groups
            .Where(g => g.ApplicationId == request.ApplicationId)
            .OrderBy(g => g.Name)
            .ToListAsync(cancellationToken);

        return entities.Select(e => new GroupDto(
            e.Id,
            e.TenantId,
            e.ApplicationId,
            e.Name,
            e.Description,
            e.IsActive,
            e.CreatedAt,
            e.CreatedBy,
            e.ModifiedAt,
            e.ModifiedBy)).ToList();
    }
}
