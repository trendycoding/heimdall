using Heimdall.Application.Common.Interfaces;
using MediatR;

namespace Heimdall.Application.AccessDetails.Commands.CreateFunctionalAreaAccessRequirement;

/// <summary>
/// Creates a functional area access requirement. Invalidates access detail cache for the application.
/// </summary>
public sealed class CreateFunctionalAreaAccessRequirementCommand : IRequest<CreateFunctionalAreaAccessRequirementResult>, ICacheInvalidatingCommand
{
    public Guid TenantId { get; init; }
    public Guid ApplicationId { get; init; }
    public Guid FunctionalAreaId { get; init; }
    public string AccessDetailType { get; init; } = string.Empty;
    public bool IsRequired { get; init; }
    public string? Description { get; init; }

    // ICacheInvalidatingCommand — FA access requirements affect access detail lookups
    public IReadOnlyList<string> GetCacheKeyPrefixes()
        => [$"access:{TenantId}:{ApplicationId}:"];
}

public sealed record CreateFunctionalAreaAccessRequirementResult(Guid Id);
