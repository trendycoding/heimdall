using Heimdall.Application.Common.Interfaces;
using Heimdall.Domain.Enums;
using MediatR;

namespace Heimdall.Application.Applications.Commands.DeactivateApplication;

/// <summary>
/// Deactivates an application. Triggers broad invalidation of all caches for the application.
/// The handler populates TenantId from the loaded entity.
/// </summary>
public sealed class DeactivateApplicationCommand : IRequest<DeactivateApplicationResult>, ICacheInvalidatingCommand
{
    public Guid ApplicationId { get; init; }

    // Populated by handler for cache invalidation
    internal Guid TenantId { get; set; }

    // ICacheInvalidatingCommand — broad invalidation for the entire application
    public IReadOnlyList<string> GetCacheKeyPrefixes()
    {
        if (TenantId == Guid.Empty)
            return [];

        return
        [
            $"perm:{TenantId}:{ApplicationId}:",
            $"perm-effective:{TenantId}:{ApplicationId}:",
            $"access:{TenantId}:{ApplicationId}:",
            $"groups:{TenantId}:{ApplicationId}:"
        ];
    }
}

public sealed record DeactivateApplicationResult(
    Guid ApplicationId,
    Guid TenantId,
    string Name,
    ApplicationStatus Status,
    DateTime? ModifiedAt,
    string? ModifiedBy);
