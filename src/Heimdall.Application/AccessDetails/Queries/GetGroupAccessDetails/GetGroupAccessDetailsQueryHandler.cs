using Heimdall.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Heimdall.Application.AccessDetails.Queries.GetGroupAccessDetails;

public sealed class GetGroupAccessDetailsQueryHandler
    : IRequestHandler<GetGroupAccessDetailsQuery, IReadOnlyList<GroupAccessDetailDto>>
{
    private readonly IHeimdallDbContext _dbContext;

    public GetGroupAccessDetailsQueryHandler(IHeimdallDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<GroupAccessDetailDto>> Handle(
        GetGroupAccessDetailsQuery request,
        CancellationToken cancellationToken)
    {
        var entities = await _dbContext.GroupAccessDetails
            .Where(d => d.GroupId == request.GroupId
                     && d.ApplicationId == request.ApplicationId)
            .OrderBy(d => d.AccessDetailType)
            .ThenBy(d => d.AccessDetailCode)
            .ToListAsync(cancellationToken);

        return entities.Select(e => new GroupAccessDetailDto(
            e.Id,
            e.TenantId,
            e.ApplicationId,
            e.GroupId,
            e.AccessDetailType,
            e.AccessDetailCode,
            e.AccessDetailValue,
            e.Description,
            e.IsActive,
            e.ValidFrom,
            e.ValidTo,
            e.CreatedAt,
            e.CreatedBy,
            e.ModifiedAt,
            e.ModifiedBy)).ToList();
    }
}
