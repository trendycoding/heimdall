using Heimdall.Application.Common.Interfaces;
using Heimdall.Domain.Entities;
using Heimdall.Domain.Enums;
using Heimdall.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Heimdall.Application.PermissionAssignments.Commands.CreateUserPermissionAssignment;

public sealed class CreateUserPermissionAssignmentCommandHandler
    : IRequestHandler<CreateUserPermissionAssignmentCommand, CreateUserPermissionAssignmentResult>
{
    private readonly IRepository<UserPermissionAssignment> _repository;
    private readonly IHeimdallDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public CreateUserPermissionAssignmentCommandHandler(
        IRepository<UserPermissionAssignment> repository,
        IHeimdallDbContext dbContext,
        ITenantContext tenantContext)
    {
        _repository = repository;
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<CreateUserPermissionAssignmentResult> Handle(
        CreateUserPermissionAssignmentCommand request,
        CancellationToken cancellationToken)
    {
        // Validate that the application exists within the caller's tenant
        var applicationExists = await _dbContext.Applications
            .AnyAsync(a => a.Id == request.ApplicationId && a.TenantId == _tenantContext.TenantId, cancellationToken);

        if (!applicationExists)
        {
            throw new InvalidOperationException($"Application '{request.ApplicationId}' does not exist or belongs to a different tenant.");
        }

        // Validate that the UserProfile exists and is active within the tenant
        var userProfile = await _dbContext.UserProfiles
            .FirstOrDefaultAsync(u => u.Id == request.UserProfileId && u.TenantId == _tenantContext.TenantId, cancellationToken);

        if (userProfile is null)
        {
            throw new InvalidOperationException($"UserProfile '{request.UserProfileId}' was not found.");
        }

        if (userProfile.Status != UserStatus.Active)
        {
            throw new InvalidOperationException($"UserProfile '{request.UserProfileId}' is inactive.");
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

        var entity = new UserPermissionAssignment
        {
            TenantId = _tenantContext.TenantId,
            ApplicationId = request.ApplicationId,
            UserProfileId = request.UserProfileId,
            PermissionId = request.PermissionId,
            Effect = request.Effect,
            ValidFrom = request.ValidFrom,
            ValidTo = request.ValidTo
        };

        await _repository.AddAsync(entity, cancellationToken);

        return new CreateUserPermissionAssignmentResult(entity.Id);
    }
}
