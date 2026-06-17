using Heimdall.Application.Common.Interfaces;
using Heimdall.Domain.Entities;
using Heimdall.Domain.Enums;
using Heimdall.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Heimdall.Application.Tenants.Commands.CreateTenant;

public sealed class CreateTenantCommandHandler : IRequestHandler<CreateTenantCommand, CreateTenantResult>
{
    private readonly IRepository<Tenant> _repository;
    private readonly IHeimdallDbContext _dbContext;

    public CreateTenantCommandHandler(IRepository<Tenant> repository, IHeimdallDbContext dbContext)
    {
        _repository = repository;
        _dbContext = dbContext;
    }

    public async Task<CreateTenantResult> Handle(CreateTenantCommand request, CancellationToken cancellationToken)
    {
        // Check slug uniqueness across all tenants (including inactive) — Requirement 1.4, 1.9
        var slugExists = await _dbContext.Tenants
            .AnyAsync(t => t.Slug == request.Slug, cancellationToken);

        if (slugExists)
        {
            throw new InvalidOperationException($"A tenant with slug '{request.Slug}' already exists.");
        }

        var tenant = new Tenant
        {
            Name = request.Name,
            Slug = request.Slug,
            PrimaryIdentityMode = request.PrimaryIdentityMode,
            Status = TenantStatus.Active
        };

        var created = await _repository.AddAsync(tenant, cancellationToken);

        return new CreateTenantResult(
            created.Id,
            created.Name,
            created.Slug,
            created.PrimaryIdentityMode,
            created.Status,
            created.CreatedAt,
            created.CreatedBy);
    }
}
