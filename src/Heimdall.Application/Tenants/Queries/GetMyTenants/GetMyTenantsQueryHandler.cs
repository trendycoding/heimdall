using Heimdall.Application.Common.Interfaces;
using Heimdall.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Heimdall.Application.Tenants.Queries.GetMyTenants;

/// <summary>
/// Finds all tenants a user belongs to by joining TenantMembership with Tenant.
/// Returns only active memberships for active tenants by default.
/// </summary>
public sealed class GetMyTenantsQueryHandler : IRequestHandler<GetMyTenantsQuery, IReadOnlyList<MyTenantResult>>
{
    private readonly IHeimdallDbContext _dbContext;

    public GetMyTenantsQueryHandler(IHeimdallDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<MyTenantResult>> Handle(GetMyTenantsQuery request, CancellationToken cancellationToken)
    {
        var results = await _dbContext.TenantMemberships
            .Where(m => m.ExternalSubjectId == request.ExternalSubjectId
                     && m.Status == MembershipStatus.Active)
            .Join(
                _dbContext.Tenants.Where(t => t.Status == TenantStatus.Active),
                membership => membership.TenantId,
                tenant => tenant.Id,
                (membership, tenant) => new MyTenantResult(
                    tenant.Id,
                    tenant.Name,
                    tenant.Slug,
                    membership.Role,
                    membership.Status,
                    tenant.Status,
                    membership.CreatedAt))
            .OrderBy(r => r.TenantName)
            .ToListAsync(cancellationToken);

        return results;
    }
}
