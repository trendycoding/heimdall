using Heimdall.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Heimdall.Application.Groups.Queries.GetGroupMembers;

public sealed class GetGroupMembersQueryHandler : IRequestHandler<GetGroupMembersQuery, IReadOnlyList<GroupMemberDto>>
{
    private readonly IHeimdallDbContext _dbContext;

    public GetGroupMembersQueryHandler(IHeimdallDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<GroupMemberDto>> Handle(GetGroupMembersQuery request, CancellationToken cancellationToken)
    {
        var memberships = await _dbContext.GroupMemberships
            .Where(gm => gm.GroupId == request.GroupId)
            .OrderBy(gm => gm.CreatedAt)
            .ToListAsync(cancellationToken);

        return memberships.Select(m => new GroupMemberDto(
            m.Id,
            m.GroupId,
            m.UserProfileId,
            m.TenantId,
            m.ApplicationId,
            m.CreatedAt,
            m.CreatedBy)).ToList();
    }
}
