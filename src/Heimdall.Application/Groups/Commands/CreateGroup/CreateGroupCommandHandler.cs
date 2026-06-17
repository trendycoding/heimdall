using Heimdall.Application.Common.Interfaces;
using Heimdall.Domain.Entities;
using Heimdall.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Heimdall.Application.Groups.Commands.CreateGroup;

public sealed class CreateGroupCommandHandler : IRequestHandler<CreateGroupCommand, CreateGroupResult>
{
    private readonly IRepository<Group> _repository;
    private readonly IHeimdallDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public CreateGroupCommandHandler(
        IRepository<Group> repository,
        IHeimdallDbContext dbContext,
        ITenantContext tenantContext)
    {
        _repository = repository;
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<CreateGroupResult> Handle(CreateGroupCommand request, CancellationToken cancellationToken)
    {
        // Validate that the application exists within the caller's tenant
        var applicationExists = await _dbContext.Applications
            .AnyAsync(a => a.Id == request.ApplicationId && a.TenantId == _tenantContext.TenantId, cancellationToken);

        if (!applicationExists)
        {
            throw new InvalidOperationException($"Application '{request.ApplicationId}' does not exist or belongs to a different tenant.");
        }

        // Enforce uniqueness of Group Name within the application
        var nameExists = await _dbContext.Groups
            .AnyAsync(g => g.ApplicationId == request.ApplicationId
                        && g.Name == request.Name, cancellationToken);

        if (nameExists)
        {
            throw new InvalidOperationException($"A Group with name '{request.Name}' already exists in this application.");
        }

        var entity = new Group
        {
            TenantId = _tenantContext.TenantId,
            ApplicationId = request.ApplicationId,
            Name = request.Name,
            Description = request.Description,
            IsActive = true
        };

        await _repository.AddAsync(entity, cancellationToken);

        return new CreateGroupResult(entity.Id);
    }
}
