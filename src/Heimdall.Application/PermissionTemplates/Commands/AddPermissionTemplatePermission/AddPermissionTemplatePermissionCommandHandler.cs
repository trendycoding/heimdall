using Heimdall.Application.Common.Interfaces;
using Heimdall.Domain.Entities;
using Heimdall.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Heimdall.Application.PermissionTemplates.Commands.AddPermissionTemplatePermission;

public sealed class AddPermissionTemplatePermissionCommandHandler
    : IRequestHandler<AddPermissionTemplatePermissionCommand, AddPermissionTemplatePermissionResult>
{
    private readonly IHeimdallDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public AddPermissionTemplatePermissionCommandHandler(
        IHeimdallDbContext dbContext,
        ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<AddPermissionTemplatePermissionResult> Handle(
        AddPermissionTemplatePermissionCommand request,
        CancellationToken cancellationToken)
    {
        // Validate that the template exists and belongs to caller's tenant
        var template = await _dbContext.PermissionTemplates
            .FirstOrDefaultAsync(pt => pt.Id == request.PermissionTemplateId, cancellationToken);

        if (template is null)
        {
            throw new InvalidOperationException($"PermissionTemplate '{request.PermissionTemplateId}' was not found.");
        }

        // Validate that the referenced Permission belongs to the same Application/Tenant — Requirement 14.6
        var permission = await _dbContext.Permissions
            .FirstOrDefaultAsync(p => p.Id == request.PermissionId, cancellationToken);

        if (permission is null || permission.ApplicationId != template.ApplicationId)
        {
            throw new InvalidOperationException(
                $"Permission '{request.PermissionId}' does not exist or belongs to a different application/tenant.");
        }

        // Enforce uniqueness of (PermissionTemplateId, PermissionId) — Requirement 14.8
        var duplicateExists = await _dbContext.PermissionTemplatePermissions
            .AnyAsync(ptp => ptp.PermissionTemplateId == request.PermissionTemplateId
                          && ptp.PermissionId == request.PermissionId, cancellationToken);

        if (duplicateExists)
        {
            throw new InvalidOperationException(
                $"A PermissionTemplatePermission entry for Permission '{request.PermissionId}' already exists in this template.");
        }

        var entity = new PermissionTemplatePermission
        {
            PermissionTemplateId = request.PermissionTemplateId,
            PermissionId = request.PermissionId,
            Effect = request.Effect,
            ValidFromOffsetDays = request.ValidFromOffsetDays,
            ValidToOffsetDays = request.ValidToOffsetDays
        };

        _dbContext.PermissionTemplatePermissions.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new AddPermissionTemplatePermissionResult(entity.Id);
    }
}
