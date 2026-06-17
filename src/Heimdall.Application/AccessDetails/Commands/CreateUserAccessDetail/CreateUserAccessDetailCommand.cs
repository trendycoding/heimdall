using Heimdall.Application.Common.Interfaces;
using MediatR;

namespace Heimdall.Application.AccessDetails.Commands.CreateUserAccessDetail;

/// <summary>
/// Creates a user access detail record. Invalidates the user's access detail cache.
/// </summary>
public sealed class CreateUserAccessDetailCommand : IRequest<CreateUserAccessDetailResult>, ICacheInvalidatingCommand
{
    public Guid TenantId { get; init; }
    public Guid ApplicationId { get; init; }
    public Guid UserProfileId { get; init; }
    public string AccessDetailType { get; init; } = string.Empty;
    public string AccessDetailCode { get; init; } = string.Empty;
    public string AccessDetailValue { get; init; } = string.Empty;
    public string? Description { get; init; }
    public DateTime? ValidFrom { get; init; }
    public DateTime? ValidTo { get; init; }

    // ICacheInvalidatingCommand
    public IReadOnlyList<string> GetCacheKeyPrefixes()
        => [$"access:{TenantId}:{ApplicationId}:{UserProfileId}:"];
}

public sealed record CreateUserAccessDetailResult(Guid Id);
