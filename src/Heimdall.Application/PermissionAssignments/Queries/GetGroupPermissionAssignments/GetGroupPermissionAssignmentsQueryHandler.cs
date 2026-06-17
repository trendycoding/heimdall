using Heimdall.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Heimdall.Application.PermissionAssignments.Queries.GetGroupPermissionAssignments;

public sealed class GetGroupPermissionAssignmentsQueryHandler
    : IRequestHandler<GetGroupPermissionAssignmentsQuery, IReadOnlyList<GroupPermissionAssignmentDto>>
{
    private readonly IHeimdallDbContext _dbContext;

    public GetGroupPermissionAssignmentsQueryHandler(IHeimdallDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<GroupPermissionAssignmentDto>> Handle(
        GetGroupPermissionAssignmentsQuery request,
        CancellationToken cancellationToken)
    {
        var entities = await _dbContext.GroupPermissionAssignments
            .Where(a => a.GroupId == request.GroupId && a.ApplicationId == request.ApplicationId)
            .OrderBy(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

        return entities.Select(e => new GroupPermissionAssignmentDto(
            e.Id,
            e.TenantId,
            e.ApplicationId,
            e.GroupId,
            e.PermissionId,
            e.Effect.ToString(),
            e.ValidFrom,
            e.ValidTo,
            e.CreatedAt,
            e.CreatedBy,
            e.ModifiedAt,
            e.ModifiedBy)).ToList();
    }
}
