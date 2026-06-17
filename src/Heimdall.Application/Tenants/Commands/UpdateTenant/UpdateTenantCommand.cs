using Heimdall.Domain.Enums;
using MediatR;

namespace Heimdall.Application.Tenants.Commands.UpdateTenant;

public sealed record UpdateTenantCommand(
    Guid TenantId,
    string Name,
    string Slug,
    PrimaryIdentityMode PrimaryIdentityMode) : IRequest<UpdateTenantResult>;

public sealed record UpdateTenantResult(
    Guid TenantId,
    string Name,
    string Slug,
    PrimaryIdentityMode PrimaryIdentityMode,
    TenantStatus Status,
    DateTime CreatedAt,
    string CreatedBy,
    DateTime? ModifiedAt,
    string? ModifiedBy);
