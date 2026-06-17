using Heimdall.Domain.Entities;
using Heimdall.Domain.Enums;
using Heimdall.Domain.Interfaces;
using MediatR;

namespace Heimdall.Application.Tenants.Commands.DeactivateTenant;

public sealed class DeactivateTenantCommandHandler : IRequestHandler<DeactivateTenantCommand, DeactivateTenantResult>
{
    private readonly IRepository<Tenant> _repository;

    public DeactivateTenantCommandHandler(IRepository<Tenant> repository)
    {
        _repository = repository;
    }

    public async Task<DeactivateTenantResult> Handle(DeactivateTenantCommand request, CancellationToken cancellationToken)
    {
        var tenant = await _repository.GetByIdAsync(request.TenantId, cancellationToken);

        if (tenant is null)
        {
            throw new KeyNotFoundException($"Tenant with ID '{request.TenantId}' was not found.");
        }

        // Reject if tenant is already Inactive (Requirement 1.8)
        if (tenant.Status == TenantStatus.Inactive)
        {
            throw new InvalidOperationException("Cannot deactivate a tenant that is already inactive.");
        }

        tenant.Status = TenantStatus.Inactive;

        await _repository.UpdateAsync(tenant, cancellationToken);

        return new DeactivateTenantResult(
            tenant.Id,
            tenant.Name,
            tenant.Slug,
            tenant.Status,
            tenant.ModifiedAt,
            tenant.ModifiedBy);
    }
}
