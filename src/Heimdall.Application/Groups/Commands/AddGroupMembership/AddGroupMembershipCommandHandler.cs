using Heimdall.Application.Common.Interfaces;
using Heimdall.Domain.Entities;
using Heimdall.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Heimdall.Application.Groups.Commands.AddGroupMembership;

public sealed class AddGroupMembershipCommandHandler : IRequestHandler<AddGroupMembershipCommand, AddGroupMembershipResult>
{
    private readonly IRepository<GroupMembership> _repository;
    private readonly IHeimdallDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public AddGroupMembershipCommandHandler(
        IRepository<GroupMembership> repository,
        IHeimdallDbContext dbContext,
        ITenantContext tenantContext)
    {
        _repository = repository;
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<AddGroupMembershipResult> Handle(AddGroupMembershipCommand request, CancellationToken cancellationToken)
    {
        // Validate that the group exists and belongs to the caller's tenant
        var group = await _dbContext.Groups
            .FirstOrDefaultAsync(g => g.Id == request.GroupId
                                   && g.ApplicationId == request.ApplicationId
                                   && g.TenantId == _tenantContext.TenantId, cancellationToken);

        if (group is null)
        {
            throw new InvalidOperationException($"Group '{request.GroupId}' does not exist in the specified application or belongs to a different tenant.");
        }

        // Validate that the user profile exists and belongs to the same tenant (cross-tenant check)
        var userProfile = await _dbContext.UserProfiles
            .FirstOrDefaultAsync(u => u.Id == request.UserProfileId
                                   && u.TenantId == _tenantContext.TenantId, cancellationToken);

        if (userProfile is null)
        {
            throw new InvalidOperationException($"UserProfile '{request.UserProfileId}' does not exist or belongs to a different tenant.");
        }

        // Enforce single membership per user per group
        var membershipExists = await _dbContext.GroupMemberships
            .AnyAsync(gm => gm.GroupId == request.GroupId
                         && gm.UserProfileId == request.UserProfileId
                         && gm.ApplicationId == request.ApplicationId, cancellationToken);

        if (membershipExists)
        {
            throw new InvalidOperationException($"UserProfile '{request.UserProfileId}' is already a member of Group '{request.GroupId}'.");
        }

        var entity = new GroupMembership
        {
            TenantId = _tenantContext.TenantId,
            ApplicationId = request.ApplicationId,
            GroupId = request.GroupId,
            UserProfileId = request.UserProfileId
        };

        await _repository.AddAsync(entity, cancellationToken);

        return new AddGroupMembershipResult(entity.Id);
    }
}
