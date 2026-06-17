using Heimdall.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Heimdall.Application.AccessDetails.Queries.GetFunctionalAreaAccessRequirements;

public sealed class GetFunctionalAreaAccessRequirementsQueryHandler
    : IRequestHandler<GetFunctionalAreaAccessRequirementsQuery, IReadOnlyList<FunctionalAreaAccessRequirementDto>>
{
    private readonly IHeimdallDbContext _dbContext;

    public GetFunctionalAreaAccessRequirementsQueryHandler(IHeimdallDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<FunctionalAreaAccessRequirementDto>> Handle(
        GetFunctionalAreaAccessRequirementsQuery request,
        CancellationToken cancellationToken)
    {
        var entities = await _dbContext.FunctionalAreaAccessRequirements
            .Where(r => r.FunctionalAreaId == request.FunctionalAreaId)
            .OrderBy(r => r.AccessDetailType)
            .ToListAsync(cancellationToken);

        return entities.Select(e => new FunctionalAreaAccessRequirementDto(
            e.Id,
            e.TenantId,
            e.ApplicationId,
            e.FunctionalAreaId,
            e.AccessDetailType,
            e.IsRequired,
            e.Description,
            e.IsActive,
            e.CreatedAt,
            e.CreatedBy,
            e.ModifiedAt,
            e.ModifiedBy)).ToList();
    }
}
