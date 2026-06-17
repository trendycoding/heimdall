using Heimdall.Application.Common.Interfaces;
using Heimdall.Domain.Entities;
using Heimdall.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Heimdall.Application.AccessDetails.Commands.CreateFunctionalAreaAccessRequirement;

public sealed class CreateFunctionalAreaAccessRequirementCommandHandler
    : IRequestHandler<CreateFunctionalAreaAccessRequirementCommand, CreateFunctionalAreaAccessRequirementResult>
{
    private readonly IRepository<FunctionalAreaAccessRequirement> _repository;
    private readonly IHeimdallDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public CreateFunctionalAreaAccessRequirementCommandHandler(
        IRepository<FunctionalAreaAccessRequirement> repository,
        IHeimdallDbContext dbContext,
        ITenantContext tenantContext)
    {
        _repository = repository;
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<CreateFunctionalAreaAccessRequirementResult> Handle(
        CreateFunctionalAreaAccessRequirementCommand request,
        CancellationToken cancellationToken)
    {
        // Validate application exists within tenant
        var applicationExists = await _dbContext.Applications
            .AnyAsync(a => a.Id == request.ApplicationId && a.TenantId == _tenantContext.TenantId, cancellationToken);

        if (!applicationExists)
        {
            throw new InvalidOperationException($"Application '{request.ApplicationId}' does not exist or belongs to a different tenant.");
        }

        // Validate functional area exists within the application
        var functionalAreaExists = await _dbContext.FunctionalAreas
            .AnyAsync(fa => fa.Id == request.FunctionalAreaId && fa.ApplicationId == request.ApplicationId, cancellationToken);

        if (!functionalAreaExists)
        {
            throw new InvalidOperationException($"FunctionalArea '{request.FunctionalAreaId}' does not exist or belongs to a different application.");
        }

        // Enforce uniqueness: FunctionalAreaId + AccessDetailType (only active) — Requirement 13.2
        var duplicateExists = await _dbContext.FunctionalAreaAccessRequirements
            .AnyAsync(r => r.FunctionalAreaId == request.FunctionalAreaId
                        && r.AccessDetailType == request.AccessDetailType
                        && r.IsActive, cancellationToken);

        if (duplicateExists)
        {
            throw new InvalidOperationException(
                $"An active FunctionalAreaAccessRequirement with type '{request.AccessDetailType}' " +
                $"already exists for FunctionalArea '{request.FunctionalAreaId}'.");
        }

        var entity = new FunctionalAreaAccessRequirement
        {
            TenantId = _tenantContext.TenantId,
            ApplicationId = request.ApplicationId,
            FunctionalAreaId = request.FunctionalAreaId,
            AccessDetailType = request.AccessDetailType,
            IsRequired = request.IsRequired,
            Description = request.Description,
            IsActive = true
        };

        await _repository.AddAsync(entity, cancellationToken);

        return new CreateFunctionalAreaAccessRequirementResult(entity.Id);
    }
}
