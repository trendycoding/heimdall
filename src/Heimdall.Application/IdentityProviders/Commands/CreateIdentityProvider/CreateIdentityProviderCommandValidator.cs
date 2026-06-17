using FluentValidation;
using Heimdall.Domain.Enums;

namespace Heimdall.Application.IdentityProviders.Commands.CreateIdentityProvider;

public sealed class CreateIdentityProviderCommandValidator : AbstractValidator<CreateIdentityProviderCommand>
{
    public CreateIdentityProviderCommandValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("TenantId is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.");

        RuleFor(x => x.Issuer)
            .NotEmpty().WithMessage("Issuer is required.");

        RuleFor(x => x.ProviderType)
            .IsInEnum().WithMessage("ProviderType must be a valid value.");

        RuleFor(x => x.ClockSkewToleranceSeconds)
            .InclusiveBetween(0, 600)
            .WithMessage("ClockSkewToleranceSeconds must be between 0 and 600.");

        RuleFor(x => x.Status)
            .IsInEnum().WithMessage("Status must be a valid IdpStatus value.");
    }
}
