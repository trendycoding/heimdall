using Heimdall.Application.Common.Interfaces;
using Heimdall.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Heimdall.Application.PermissionAssignments.Commands.DeleteUserPermissionAssignment;

public sealed class DeleteUserPermissionAssignmentCommandHandler
    : IRequestHandler<DeleteUserPermissionAssignmentCommand, Unit>
{
    private readonly IHeimdallDbContext _dbContext;

    public DeleteUserPermissionAssignmentCommandHandler(IHeimdallDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Unit> Handle(DeleteUserPermissionAssignmentCommand request, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.UserPermissionAssignments
            .FirstOrDefaultAsync(a => a.Id == request.Id, cancellationToken);

        if (entity is null)
        {
            throw new InvalidOperationException($"UserPermissionAssignment '{request.Id}' was not found.");
        }

        // Populate cache invalidation context before deletion
        request.TenantId = entity.TenantId;
        request.ApplicationId = entity.ApplicationId;
        request.UserProfileId = entity.UserProfileId;

        _dbContext.UserPermissionAssignments.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
