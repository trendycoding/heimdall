using FluentValidation;

namespace Heimdall.Application.IdentityProviders.Commands.DeactivateIdentityProvider;

public sealed class DeactivateIdentityProviderCommandValidator : AbstractValidator<DeactivateIdentityProviderCommand>
{
    public DeactivateIdentityProviderCommandValidator()
    {
        RuleFor(x => x.ProviderId)
            .NotEmpty().WithMessage("ProviderId is required.");
    }
}
