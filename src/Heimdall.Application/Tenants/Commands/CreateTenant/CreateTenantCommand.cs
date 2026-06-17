using Heimdall.Domain.Enums;
using MediatR;

namespace Heimdall.Application.Tenants.Commands.CreateTenant;

public sealed record CreateTenantCommand(
    string Name,
    string Slug,
    PrimaryIdentityMode PrimaryIdentityMode) : IRequest<CreateTenantResult>;

public sealed record CreateTenantResult(
    Guid TenantId,
    string Name,
    string Slug,
    PrimaryIdentityMode PrimaryIdentityMode,
    TenantStatus Status,
    DateTime CreatedAt,
    string CreatedBy);
