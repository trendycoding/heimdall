using Heimdall.Application.Common.Interfaces;
using Heimdall.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Heimdall.Application.Groups.Commands.RemoveGroupMembership;

public sealed class RemoveGroupMembershipCommandHandler : IRequestHandler<RemoveGroupMembershipCommand, Unit>
{
    private readonly IHeimdallDbContext _dbContext;

    public RemoveGroupMembershipCommandHandler(IHeimdallDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Unit> Handle(RemoveGroupMembershipCommand request, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.GroupMemberships
            .FirstOrDefaultAsync(gm => gm.Id == request.Id, cancellationToken);

        if (entity is null)
        {
            throw new InvalidOperationException($"GroupMembership '{request.Id}' was not found.");
        }

        // Populate cache invalidation context before deletion
        request.TenantId = entity.TenantId;
        request.ApplicationId = entity.ApplicationId;
        request.UserProfileId = entity.UserProfileId;

        _dbContext.GroupMemberships.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
