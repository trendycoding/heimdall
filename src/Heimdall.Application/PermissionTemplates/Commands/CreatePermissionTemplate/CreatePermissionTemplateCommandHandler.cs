using Heimdall.Application.Common.Interfaces;
using Heimdall.Domain.Entities;
using Heimdall.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Heimdall.Application.PermissionTemplates.Commands.CreatePermissionTemplate;

public sealed class CreatePermissionTemplateCommandHandler : IRequestHandler<CreatePermissionTemplateCommand, CreatePermissionTemplateResult>
{
    private readonly IRepository<PermissionTemplate> _repository;
    private readonly IHeimdallDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public CreatePermissionTemplateCommandHandler(
        IRepository<PermissionTemplate> repository,
        IHeimdallDbContext dbContext,
        ITenantContext tenantContext)
    {
        _repository = repository;
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<CreatePermissionTemplateResult> Handle(CreatePermissionTemplateCommand request, CancellationToken cancellationToken)
    {
        // Validate that the application exists within the caller's tenant
        var applicationExists = await _dbContext.Applications
            .AnyAsync(a => a.Id == request.ApplicationId && a.TenantId == _tenantContext.TenantId, cancellationToken);

        if (!applicationExists)
        {
            throw new InvalidOperationException($"Application '{request.ApplicationId}' does not exist or belongs to a different tenant.");
        }

        // Enforce uniqueness of TemplateCode within the application — Requirement 14.5
        var codeExists = await _dbContext.PermissionTemplates
            .AnyAsync(pt => pt.ApplicationId == request.ApplicationId
                         && pt.TemplateCode == request.TemplateCode, cancellationToken);

        if (codeExists)
        {
            throw new InvalidOperationException($"A PermissionTemplate with code '{request.TemplateCode}' already exists in this application.");
        }

        var entity = new PermissionTemplate
        {
            TenantId = _tenantContext.TenantId,
            ApplicationId = request.ApplicationId,
            TemplateCode = request.TemplateCode,
            Name = request.Name,
            Description = request.Description,
            IsActive = true
        };

        await _repository.AddAsync(entity, cancellationToken);

        return new CreatePermissionTemplateResult(entity.Id);
    }
}
