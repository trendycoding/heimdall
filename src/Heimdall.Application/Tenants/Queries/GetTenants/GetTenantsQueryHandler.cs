using Heimdall.Domain.Entities;
using Heimdall.Domain.Interfaces;
using MediatR;

namespace Heimdall.Application.Tenants.Queries.GetTenants;

public sealed class GetTenantsQueryHandler : IRequestHandler<GetTenantsQuery, IReadOnlyList<GetTenantsResult>>
{
    private readonly IRepository<Tenant> _repository;

    public GetTenantsQueryHandler(IRepository<Tenant> repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<GetTenantsResult>> Handle(GetTenantsQuery request, CancellationToken cancellationToken)
    {
        var tenants = await _repository.GetAllAsync(cancellationToken);

        return tenants.Select(t => new GetTenantsResult(
            t.Id,
            t.Name,
            t.Slug,
            t.PrimaryIdentityMode,
            t.Status,
            t.CreatedAt,
            t.CreatedBy,
            t.ModifiedAt,
            t.ModifiedBy)).ToList();
    }
}
