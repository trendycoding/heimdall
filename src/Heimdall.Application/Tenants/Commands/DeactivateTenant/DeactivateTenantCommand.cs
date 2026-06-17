using Heimdall.Domain.Enums;
using MediatR;

namespace Heimdall.Application.Tenants.Commands.DeactivateTenant;

public sealed record DeactivateTenantCommand(Guid TenantId) : IRequest<DeactivateTenantResult>;

public sealed record DeactivateTenantResult(
    Guid TenantId,
    string Name,
    string Slug,
    TenantStatus Status,
    DateTime? ModifiedAt,
    string? ModifiedBy);
