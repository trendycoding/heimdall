using Heimdall.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Heimdall.Application.AccessDetails.Queries.GetUserAccessDetails;

public sealed class GetUserAccessDetailsQueryHandler
    : IRequestHandler<GetUserAccessDetailsQuery, IReadOnlyList<UserAccessDetailDto>>
{
    private readonly IHeimdallDbContext _dbContext;

    public GetUserAccessDetailsQueryHandler(IHeimdallDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<UserAccessDetailDto>> Handle(
        GetUserAccessDetailsQuery request,
        CancellationToken cancellationToken)
    {
        var entities = await _dbContext.UserAccessDetails
            .Where(d => d.UserProfileId == request.UserProfileId
                     && d.ApplicationId == request.ApplicationId)
            .OrderBy(d => d.AccessDetailType)
            .ThenBy(d => d.AccessDetailCode)
            .ToListAsync(cancellationToken);

        return entities.Select(e => new UserAccessDetailDto(
            e.Id,
            e.TenantId,
            e.ApplicationId,
            e.UserProfileId,
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
