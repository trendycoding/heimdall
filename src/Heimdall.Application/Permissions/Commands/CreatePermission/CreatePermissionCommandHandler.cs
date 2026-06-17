using Heimdall.Application.Common.Interfaces;
using Heimdall.Domain.Entities;
using Heimdall.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Heimdall.Application.Permissions.Commands.CreatePermission;

public sealed class CreatePermissionCommandHandler : IRequestHandler<CreatePermissionCommand, CreatePermissionResult>
{
    private readonly IRepository<Permission> _repository;
    private readonly IHeimdallDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public CreatePermissionCommandHandler(
        IRepository<Permission> repository,
        IHeimdallDbContext dbContext,
        ITenantContext tenantContext)
    {
        _repository = repository;
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<CreatePermissionResult> Handle(CreatePermissionCommand request, CancellationToken cancellationToken)
    {
        // Validate that the application exists within the caller's tenant
        var applicationExists = await _dbContext.Applications
            .AnyAsync(a => a.Id == request.ApplicationId && a.TenantId == _tenantContext.TenantId, cancellationToken);

        if (!applicationExists)
        {
            throw new InvalidOperationException($"Application '{request.ApplicationId}' does not exist or belongs to a different tenant.");
        }

        // Validate that the FunctionalArea belongs to the same application
        var functionalArea = await _dbContext.FunctionalAreas
            .FirstOrDefaultAsync(fa => fa.Id == request.FunctionalAreaId, cancellationToken);

        if (functionalArea is null || functionalArea.ApplicationId != request.ApplicationId)
        {
            throw new InvalidOperationException($"FunctionalArea '{request.FunctionalAreaId}' does not exist or belongs to a different application.");
        }

        // Validate that the PermissionType belongs to the same application and is active
        var permissionType = await _dbContext.PermissionTypes
            .FirstOrDefaultAsync(pt => pt.Id == request.PermissionTypeId, cancellationToken);

        if (permissionType is null || permissionType.ApplicationId != request.ApplicationId)
        {
            throw new InvalidOperationException($"PermissionType '{request.PermissionTypeId}' does not exist or belongs to a different application.");
        }

        if (!permissionType.IsActive)
        {
            throw new InvalidOperationException("Inactive permission types cannot be assigned to new permissions.");
        }

        // Enforce uniqueness of PermissionCode within the application
        var codeExists = await _dbContext.Permissions
            .AnyAsync(p => p.ApplicationId == request.ApplicationId
                        && p.PermissionCode == request.PermissionCode, cancellationToken);

        if (codeExists)
        {
            throw new InvalidOperationException($"A Permission with code '{request.PermissionCode}' already exists in this application.");
        }

        var entity = new Permission
        {
            TenantId = _tenantContext.TenantId,
            ApplicationId = request.ApplicationId,
            FunctionalAreaId = request.FunctionalAreaId,
            PermissionTypeId = request.PermissionTypeId,
            PermissionCode = request.PermissionCode,
            Name = request.Name,
            Description = request.Description,
            IsActive = true
        };

        await _repository.AddAsync(entity, cancellationToken);

        return new CreatePermissionResult(entity.Id);
    }
}
