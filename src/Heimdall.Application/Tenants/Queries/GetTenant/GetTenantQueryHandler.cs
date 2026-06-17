using Heimdall.Domain.Entities;
using Heimdall.Domain.Interfaces;
using MediatR;

namespace Heimdall.Application.Tenants.Queries.GetTenant;

public sealed class GetTenantQueryHandler : IRequestHandler<GetTenantQuery, GetTenantResult?>
{
    private readonly IRepository<Tenant> _repository;

    public GetTenantQueryHandler(IRepository<Tenant> repository)
    {
        _repository = repository;
    }

    public async Task<GetTenantResult?> Handle(GetTenantQuery request, CancellationToken cancellationToken)
    {
        var tenant = await _repository.GetByIdAsync(request.TenantId, cancellationToken);

        if (tenant is null)
        {
            return null;
        }

        return new GetTenantResult(
            tenant.Id,
            tenant.Name,
            tenant.Slug,
            tenant.PrimaryIdentityMode,
            tenant.Status,
            tenant.CreatedAt,
            tenant.CreatedBy,
            tenant.ModifiedAt,
            tenant.ModifiedBy);
    }
}
