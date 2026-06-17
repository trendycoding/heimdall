using Heimdall.Application.Common.Interfaces;
using Heimdall.Domain.Entities;
using Heimdall.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Heimdall.Application.FunctionalAreas.Commands.CreateFunctionalArea;

public sealed class CreateFunctionalAreaCommandHandler : IRequestHandler<CreateFunctionalAreaCommand, CreateFunctionalAreaResult>
{
    private readonly IRepository<FunctionalArea> _repository;
    private readonly IHeimdallDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public CreateFunctionalAreaCommandHandler(
        IRepository<FunctionalArea> repository,
        IHeimdallDbContext dbContext,
        ITenantContext tenantContext)
    {
        _repository = repository;
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<CreateFunctionalAreaResult> Handle(CreateFunctionalAreaCommand request, CancellationToken cancellationToken)
    {
        // Validate that the application exists within the caller's tenant
        var applicationExists = await _dbContext.Applications
            .AnyAsync(a => a.Id == request.ApplicationId && a.TenantId == _tenantContext.TenantId, cancellationToken);

        if (!applicationExists)
        {
            throw new InvalidOperationException($"Application '{request.ApplicationId}' does not exist or belongs to a different tenant.");
        }

        // Enforce uniqueness of FunctionalAreaCode within the application
        var codeExists = await _dbContext.FunctionalAreas
            .AnyAsync(fa => fa.ApplicationId == request.ApplicationId
                         && fa.FunctionalAreaCode == request.FunctionalAreaCode, cancellationToken);

        if (codeExists)
        {
            throw new InvalidOperationException($"A FunctionalArea with code '{request.FunctionalAreaCode}' already exists in this application.");
        }

        var entity = new FunctionalArea
        {
            TenantId = _tenantContext.TenantId,
            ApplicationId = request.ApplicationId,
            FunctionalAreaCode = request.FunctionalAreaCode,
            Name = request.Name,
            Description = request.Description,
            IsActive = true
        };

        await _repository.AddAsync(entity, cancellationToken);

        return new CreateFunctionalAreaResult(entity.Id);
    }
}
