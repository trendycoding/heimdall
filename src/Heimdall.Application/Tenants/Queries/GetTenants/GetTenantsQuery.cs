using Heimdall.Domain.Enums;
using MediatR;

namespace Heimdall.Application.Tenants.Queries.GetTenants;

public sealed record GetTenantsQuery : IRequest<IReadOnlyList<GetTenantsResult>>;

public sealed record GetTenantsResult(
    Guid TenantId,
    string Name,
    string Slug,
    PrimaryIdentityMode PrimaryIdentityMode,
    TenantStatus Status,
    DateTime CreatedAt,
    string CreatedBy,
    DateTime? ModifiedAt,
    string? ModifiedBy);
