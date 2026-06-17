using Heimdall.Application.Common.Interfaces;
using Heimdall.Domain.Entities;
using Heimdall.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Heimdall.Application.PermissionTemplates.Commands.AddPermissionTemplateGroup;

public sealed class AddPermissionTemplateGroupCommandHandler
    : IRequestHandler<AddPermissionTemplateGroupCommand, AddPermissionTemplateGroupResult>
{
    private readonly IHeimdallDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public AddPermissionTemplateGroupCommandHandler(
        IHeimdallDbContext dbContext,
        ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<AddPermissionTemplateGroupResult> Handle(
        AddPermissionTemplateGroupCommand request,
        CancellationToken cancellationToken)
    {
        // Validate that the template exists and belongs to caller's tenant
        var template = await _dbContext.PermissionTemplates
            .FirstOrDefaultAsync(pt => pt.Id == request.PermissionTemplateId, cancellationToken);

        if (template is null)
        {
            throw new InvalidOperationException($"PermissionTemplate '{request.PermissionTemplateId}' was not found.");
        }

        // Validate that the referenced Group belongs to the same Application/Tenant — Requirement 14.6
        var group = await _dbContext.Groups
            .FirstOrDefaultAsync(g => g.Id == request.GroupId, cancellationToken);

        if (group is null || group.ApplicationId != template.ApplicationId)
        {
            throw new InvalidOperationException(
                $"Group '{request.GroupId}' does not exist or belongs to a different application/tenant.");
        }

        // Enforce uniqueness of (PermissionTemplateId, GroupId)
        var duplicateExists = await _dbContext.PermissionTemplateGroups
            .AnyAsync(ptg => ptg.PermissionTemplateId == request.PermissionTemplateId
                          && ptg.GroupId == request.GroupId, cancellationToken);

        if (duplicateExists)
        {
            throw new InvalidOperationException(
                $"A PermissionTemplateGroup entry for Group '{request.GroupId}' already exists in this template.");
        }

        var entity = new PermissionTemplateGroup
        {
            PermissionTemplateId = request.PermissionTemplateId,
            GroupId = request.GroupId
        };

        _dbContext.PermissionTemplateGroups.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new AddPermissionTemplateGroupResult(entity.Id);
    }
}
