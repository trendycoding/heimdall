using Heimdall.Application.Common.Interfaces;
using Heimdall.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Heimdall.Application.PermissionAssignments.Commands.DeleteGroupPermissionAssignment;

public sealed class DeleteGroupPermissionAssignmentCommandHandler
    : IRequestHandler<DeleteGroupPermissionAssignmentCommand, Unit>
{
    private readonly IHeimdallDbContext _dbContext;

    public DeleteGroupPermissionAssignmentCommandHandler(IHeimdallDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Unit> Handle(DeleteGroupPermissionAssignmentCommand request, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.GroupPermissionAssignments
            .FirstOrDefaultAsync(a => a.Id == request.Id, cancellationToken);

        if (entity is null)
        {
            throw new InvalidOperationException($"GroupPermissionAssignment '{request.Id}' was not found.");
        }

        // Populate cache invalidation context before deletion
        request.TenantId = entity.TenantId;
        request.ApplicationId = entity.ApplicationId;

        _dbContext.GroupPermissionAssignments.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
