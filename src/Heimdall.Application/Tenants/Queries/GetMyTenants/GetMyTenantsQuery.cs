using Heimdall.Domain.Enums;
using MediatR;

namespace Heimdall.Application.Tenants.Queries.GetMyTenants;

/// <summary>
/// Retrieves all tenants the current user has membership in.
/// This is pre-tenant-scoped — it uses the external subject ID from the auth token.
/// </summary>
public sealed record GetMyTenantsQuery(string ExternalSubjectId) : IRequest<IReadOnlyList<MyTenantResult>>;

public sealed record MyTenantResult(
    Guid TenantId,
    string TenantName,
    string Slug,
    TenantRole Role,
    MembershipStatus MembershipStatus,
    TenantStatus TenantStatus,
    DateTime MemberSince);
