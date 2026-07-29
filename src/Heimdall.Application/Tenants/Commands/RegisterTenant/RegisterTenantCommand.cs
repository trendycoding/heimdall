using Heimdall.Domain.Enums;
using MediatR;

namespace Heimdall.Application.Tenants.Commands.RegisterTenant;

/// <summary>
/// Self-service tenant registration command.
/// Creates a new tenant and assigns the requesting user as the Owner.
/// Does not require an existing tenant context — this is pre-tenant-scoped.
/// </summary>
public sealed record RegisterTenantCommand(
    string TenantName,
    string Slug,
    PrimaryIdentityMode PrimaryIdentityMode,
    string ExternalSubjectId,
    string Email,
    string DisplayName) : IRequest<RegisterTenantResult>;

public sealed record RegisterTenantResult(
    Guid TenantId,
    string TenantName,
    string Slug,
    Guid MembershipId,
    TenantRole Role);
