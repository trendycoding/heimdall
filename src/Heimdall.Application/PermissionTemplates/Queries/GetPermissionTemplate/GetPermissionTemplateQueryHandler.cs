using Heimdall.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Heimdall.Application.PermissionTemplates.Queries.GetPermissionTemplate;

public sealed class GetPermissionTemplateQueryHandler
    : IRequestHandler<GetPermissionTemplateQuery, PermissionTemplateDto?>
{
    private readonly IHeimdallDbContext _dbContext;

    public GetPermissionTemplateQueryHandler(IHeimdallDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PermissionTemplateDto?> Handle(GetPermissionTemplateQuery request, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.PermissionTemplates
            .FirstOrDefaultAsync(pt => pt.Id == request.Id, cancellationToken);

        if (entity is null)
            return null;

        var permissions = await _dbContext.PermissionTemplatePermissions
            .Where(ptp => ptp.PermissionTemplateId == request.Id)
            .Select(ptp => new PermissionTemplatePermissionDto(
                ptp.Id,
                ptp.PermissionId,
                ptp.Effect,
                ptp.ValidFromOffsetDays,
                ptp.ValidToOffsetDays))
            .ToListAsync(cancellationToken);

        var groups = await _dbContext.PermissionTemplateGroups
            .Where(ptg => ptg.PermissionTemplateId == request.Id)
            .Select(ptg => new PermissionTemplateGroupDto(
                ptg.Id,
                ptg.GroupId))
            .ToListAsync(cancellationToken);

        var accessDetails = await _dbContext.PermissionTemplateAccessDetails
            .Where(ptad => ptad.PermissionTemplateId == request.Id)
            .Select(ptad => new PermissionTemplateAccessDetailDto(
                ptad.Id,
                ptad.AccessDetailType,
                ptad.AccessDetailCode,
                ptad.AccessDetailValue,
                ptad.Description,
                ptad.ValidFromOffsetDays,
                ptad.ValidToOffsetDays))
            .ToListAsync(cancellationToken);

        return new PermissionTemplateDto(
            entity.Id,
            entity.TenantId,
            entity.ApplicationId,
            entity.TemplateCode,
            entity.Name,
            entity.Description,
            entity.IsActive,
            entity.CreatedAt,
            entity.CreatedBy,
            entity.ModifiedAt,
            entity.ModifiedBy,
            permissions,
            groups,
            accessDetails);
    }
}
