using Heimdall.Application.Common.Interfaces;
using Heimdall.Domain.Entities;
using Heimdall.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Heimdall.Application.PermissionAssignments.Commands.CreateGroupPermissionAssignment;

public sealed class CreateGroupPermissionAssignmentCommandHandler
    : IRequestHandler<CreateGroupPermissionAssignmentCommand, CreateGroupPermissionAssignmentResult>
{
    private readonly IRepository<GroupPermissionAssignment> _repository;
    private readonly IHeimdallDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public CreateGroupPermissionAssignmentCommandHandler(
        IRepository<GroupPermissionAssignment> repository,
        IHeimdallDbContext dbContext,
        ITenantContext tenantContext)
    {
        _repository = repository;
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<CreateGroupPermissionAssignmentResult> Handle(
        CreateGroupPermissionAssignmentCommand request,
        CancellationToken cancellationToken)
    {
        // Validate that the application exists within the caller's tenant
        var applicationExists = await _dbContext.Applications
            .AnyAsync(a => a.Id == request.ApplicationId && a.TenantId == _tenantContext.TenantId, cancellationToken);

        if (!applicationExists)
        {
            throw new InvalidOperationException($"Application '{request.ApplicationId}' does not exist or belongs to a different tenant.");
        }

        // Validate that the Group exists, belongs to the same application, and is active
        var group = await _dbContext.Groups
            .FirstOrDefaultAsync(g => g.Id == request.GroupId && g.ApplicationId == request.ApplicationId, cancellationToken);

        if (group is null)
        {
            throw new InvalidOperationException($"Group '{request.GroupId}' does not exist or belongs to a different application.");
        }

        if (!group.IsActive)
        {
            throw new InvalidOperationException($"Group '{request.GroupId}' is inactive.");
        }

        // Validate that the Permission exists, belongs to the same application, and is active
        var permission = await _dbContext.Permissions
            .FirstOrDefaultAsync(p => p.Id == request.PermissionId, cancellationToken);

        if (permission is null || permission.ApplicationId != request.ApplicationId)
        {
            throw new InvalidOperationException($"Permission '{request.PermissionId}' does not exist or belongs to a different application.");
        }

        if (!permission.IsActive)
        {
            throw new InvalidOperationException("Inactive permissions cannot be newly assigned.");
        }

        var entity = new GroupPermissionAssignment
        {
            TenantId = _tenantContext.TenantId,
            ApplicationId = request.ApplicationId,
            GroupId = request.GroupId,
            PermissionId = request.PermissionId,
            Effect = request.Effect,
            ValidFrom = request.ValidFrom,
            ValidTo = request.ValidTo
        };

        await _repository.AddAsync(entity, cancellationToken);

        return new CreateGroupPermissionAssignmentResult(entity.Id);
    }
}
