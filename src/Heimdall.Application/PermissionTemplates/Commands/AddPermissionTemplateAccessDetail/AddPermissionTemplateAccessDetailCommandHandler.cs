using Heimdall.Application.Common.Interfaces;
using Heimdall.Domain.Entities;
using Heimdall.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Heimdall.Application.PermissionTemplates.Commands.AddPermissionTemplateAccessDetail;

public sealed class AddPermissionTemplateAccessDetailCommandHandler
    : IRequestHandler<AddPermissionTemplateAccessDetailCommand, AddPermissionTemplateAccessDetailResult>
{
    private readonly IHeimdallDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public AddPermissionTemplateAccessDetailCommandHandler(
        IHeimdallDbContext dbContext,
        ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<AddPermissionTemplateAccessDetailResult> Handle(
        AddPermissionTemplateAccessDetailCommand request,
        CancellationToken cancellationToken)
    {
        // Validate that the template exists and belongs to caller's tenant
        var template = await _dbContext.PermissionTemplates
            .FirstOrDefaultAsync(pt => pt.Id == request.PermissionTemplateId, cancellationToken);

        if (template is null)
        {
            throw new InvalidOperationException($"PermissionTemplate '{request.PermissionTemplateId}' was not found.");
        }

        // Enforce uniqueness of (PermissionTemplateId, AccessDetailType, AccessDetailCode)
        var duplicateExists = await _dbContext.PermissionTemplateAccessDetails
            .AnyAsync(ptad => ptad.PermissionTemplateId == request.PermissionTemplateId
                           && ptad.AccessDetailType == request.AccessDetailType
                           && ptad.AccessDetailCode == request.AccessDetailCode, cancellationToken);

        if (duplicateExists)
        {
            throw new InvalidOperationException(
                $"A PermissionTemplateAccessDetail entry with type '{request.AccessDetailType}' and code '{request.AccessDetailCode}' already exists in this template.");
        }

        var entity = new PermissionTemplateAccessDetail
        {
            PermissionTemplateId = request.PermissionTemplateId,
            AccessDetailType = request.AccessDetailType,
            AccessDetailCode = request.AccessDetailCode,
            AccessDetailValue = request.AccessDetailValue,
            Description = request.Description,
            ValidFromOffsetDays = request.ValidFromOffsetDays,
            ValidToOffsetDays = request.ValidToOffsetDays
        };

        _dbContext.PermissionTemplateAccessDetails.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new AddPermissionTemplateAccessDetailResult(entity.Id);
    }
}
