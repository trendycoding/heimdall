using FluentValidation;

namespace Heimdall.Application.Applications.Commands.DeactivateApplication;

public sealed class DeactivateApplicationCommandValidator : AbstractValidator<DeactivateApplicationCommand>
{
    public DeactivateApplicationCommandValidator()
    {
        RuleFor(x => x.ApplicationId)
            .NotEmpty().WithMessage("ApplicationId is required.");
    }
}
