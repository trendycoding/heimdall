using Heimdall.Domain.Enums;
using MediatR;

namespace Heimdall.Application.Tenants.Queries.GetTenant;

public sealed record GetTenantQuery(Guid TenantId) : IRequest<GetTenantResult?>;

public sealed record GetTenantResult(
    Guid TenantId,
    string Name,
    string Slug,
    PrimaryIdentityMode PrimaryIdentityMode,
    TenantStatus Status,
    DateTime CreatedAt,
    string CreatedBy,
    DateTime? ModifiedAt,
    string? ModifiedBy);
