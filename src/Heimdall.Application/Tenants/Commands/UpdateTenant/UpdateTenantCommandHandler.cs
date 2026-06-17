using Heimdall.Application.Common.Interfaces;
using Heimdall.Domain.Entities;
using Heimdall.Domain.Enums;
using Heimdall.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Heimdall.Application.Tenants.Commands.UpdateTenant;

public sealed class UpdateTenantCommandHandler : IRequestHandler<UpdateTenantCommand, UpdateTenantResult>
{
    private readonly IRepository<Tenant> _repository;
    private readonly IHeimdallDbContext _dbContext;

    public UpdateTenantCommandHandler(IRepository<Tenant> repository, IHeimdallDbContext dbContext)
    {
        _repository = repository;
        _dbContext = dbContext;
    }

    public async Task<UpdateTenantResult> Handle(UpdateTenantCommand request, CancellationToken cancellationToken)
    {
        var tenant = await _repository.GetByIdAsync(request.TenantId, cancellationToken);

        if (tenant is null)
        {
            throw new KeyNotFoundException($"Tenant with ID '{request.TenantId}' was not found.");
        }

        // Reject if tenant is Inactive (Requirement 1.8)
        if (tenant.Status == TenantStatus.Inactive)
        {
            throw new InvalidOperationException("Cannot update an inactive tenant.");
        }

        // Check slug uniqueness if slug is being changed (Requirement 1.4, 1.9)
        if (!string.Equals(tenant.Slug, request.Slug, StringComparison.Ordinal))
        {
            var slugExists = await _dbContext.Tenants
                .AnyAsync(t => t.Slug == request.Slug && t.Id != request.TenantId, cancellationToken);

            if (slugExists)
            {
                throw new InvalidOperationException($"A tenant with slug '{request.Slug}' already exists.");
            }
        }

        // Update only mutable fields; preserve immutable fields (TenantId, CreatedAt, CreatedBy) — Requirement 1.2
        tenant.Name = request.Name;
        tenant.Slug = request.Slug;
        tenant.PrimaryIdentityMode = request.PrimaryIdentityMode;

        await _repository.UpdateAsync(tenant, cancellationToken);

        return new UpdateTenantResult(
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
