using Heimdall.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Heimdall.Application.PermissionTemplates.Queries.GetPermissionTemplates;

public sealed class GetPermissionTemplatesQueryHandler
    : IRequestHandler<GetPermissionTemplatesQuery, IReadOnlyList<PermissionTemplateSummaryDto>>
{
    private readonly IHeimdallDbContext _dbContext;

    public GetPermissionTemplatesQueryHandler(IHeimdallDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<PermissionTemplateSummaryDto>> Handle(
        GetPermissionTemplatesQuery request,
        CancellationToken cancellationToken)
    {
        var entities = await _dbContext.PermissionTemplates
            .Where(pt => pt.ApplicationId == request.ApplicationId)
            .OrderBy(pt => pt.TemplateCode)
            .ToListAsync(cancellationToken);

        return entities.Select(e => new PermissionTemplateSummaryDto(
            e.Id,
            e.TenantId,
            e.ApplicationId,
            e.TemplateCode,
            e.Name,
            e.Description,
            e.IsActive,
            e.CreatedAt,
            e.CreatedBy,
            e.ModifiedAt,
            e.ModifiedBy)).ToList();
    }
}
