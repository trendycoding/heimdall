using Heimdall.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Heimdall.Application.PermissionAssignments.Queries.GetUserPermissionAssignments;

public sealed class GetUserPermissionAssignmentsQueryHandler
    : IRequestHandler<GetUserPermissionAssignmentsQuery, IReadOnlyList<UserPermissionAssignmentDto>>
{
    private readonly IHeimdallDbContext _dbContext;

    public GetUserPermissionAssignmentsQueryHandler(IHeimdallDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<UserPermissionAssignmentDto>> Handle(
        GetUserPermissionAssignmentsQuery request,
        CancellationToken cancellationToken)
    {
        var entities = await _dbContext.UserPermissionAssignments
            .Where(a => a.UserProfileId == request.UserProfileId && a.ApplicationId == request.ApplicationId)
            .OrderBy(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

        return entities.Select(e => new UserPermissionAssignmentDto(
            e.Id,
            e.TenantId,
            e.ApplicationId,
            e.UserProfileId,
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
