using Heimdall.Application.Common.Interfaces;
using Heimdall.Application.FunctionalAreas.Queries.GetFunctionalArea;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Heimdall.Application.FunctionalAreas.Queries.GetFunctionalAreas;

public sealed class GetFunctionalAreasQueryHandler : IRequestHandler<GetFunctionalAreasQuery, IReadOnlyList<FunctionalAreaDto>>
{
    private readonly IHeimdallDbContext _dbContext;

    public GetFunctionalAreasQueryHandler(IHeimdallDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<FunctionalAreaDto>> Handle(GetFunctionalAreasQuery request, CancellationToken cancellationToken)
    {
        var entities = await _dbContext.FunctionalAreas
            .Where(fa => fa.ApplicationId == request.ApplicationId)
            .OrderBy(fa => fa.FunctionalAreaCode)
            .ToListAsync(cancellationToken);

        return entities.Select(e => new FunctionalAreaDto(
            e.Id,
            e.TenantId,
            e.ApplicationId,
            e.FunctionalAreaCode,
            e.Name,
            e.Description,
            e.IsActive,
            e.CreatedAt,
            e.CreatedBy,
            e.ModifiedAt,
            e.ModifiedBy)).ToList();
    }
}
