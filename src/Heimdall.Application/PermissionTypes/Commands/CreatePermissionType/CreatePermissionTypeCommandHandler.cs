using Heimdall.Application.Common.Interfaces;
using Heimdall.Domain.Entities;
using Heimdall.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Heimdall.Application.PermissionTypes.Commands.CreatePermissionType;

public sealed class CreatePermissionTypeCommandHandler : IRequestHandler<CreatePermissionTypeCommand, CreatePermissionTypeResult>
{
    private readonly IRepository<PermissionType> _repository;
    private readonly IHeimdallDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public CreatePermissionTypeCommandHandler(
        IRepository<PermissionType> repository,
        IHeimdallDbContext dbContext,
        ITenantContext tenantContext)
    {
        _repository = repository;
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<CreatePermissionTypeResult> Handle(CreatePermissionTypeCommand request, CancellationToken cancellationToken)
    {
        // Validate that the application exists within the caller's tenant
        var applicationExists = await _dbContext.Applications
            .AnyAsync(a => a.Id == request.ApplicationId && a.TenantId == _tenantContext.TenantId, cancellationToken);

        if (!applicationExists)
        {
            throw new InvalidOperationException($"Application '{request.ApplicationId}' does not exist or belongs to a different tenant.");
        }

        // Enforce case-insensitive uniqueness of Code within the application
        var codeExists = await _dbContext.PermissionTypes
            .AnyAsync(pt => pt.ApplicationId == request.ApplicationId
                         && pt.Code.ToLower() == request.Code.ToLower(), cancellationToken);

        if (codeExists)
        {
            throw new InvalidOperationException($"A PermissionType with code '{request.Code}' already exists in this application.");
        }

        var entity = new PermissionType
        {
            TenantId = _tenantContext.TenantId,
            ApplicationId = request.ApplicationId,
            Code = request.Code,
            Name = request.Name,
            Description = request.Description,
            IsSystemReserved = request.IsSystemReserved,
            IsActive = true
        };

        await _repository.AddAsync(entity, cancellationToken);

        return new CreatePermissionTypeResult(entity.Id);
    }
}
