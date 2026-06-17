using MediatR;

namespace Heimdall.Application.IdentityProviders.Commands.DeactivateIdentityProvider;

public sealed record DeactivateIdentityProviderCommand(
    Guid ProviderId) : IRequest<DeactivateIdentityProviderResult>;

public sealed record DeactivateIdentityProviderResult(
    Guid ProviderId,
    DateTime? ModifiedAt,
    string? ModifiedBy);
