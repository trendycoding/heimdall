using FluentValidation;

namespace Heimdall.Application.FunctionalAreas.Commands.CreateFunctionalArea;

public sealed class CreateFunctionalAreaCommandValidator : AbstractValidator<CreateFunctionalAreaCommand>
{
    public CreateFunctionalAreaCommandValidator()
    {
        RuleFor(x => x.ApplicationId)
            .NotEmpty()
            .WithMessage("ApplicationId is required.");

        RuleFor(x => x.FunctionalAreaCode)
            .NotEmpty()
            .WithMessage("FunctionalAreaCode is required.")
            .MaximumLength(50)
            .WithMessage("FunctionalAreaCode must not exceed 50 characters.")
            .Matches(@"^[A-Z0-9_]+$")
            .WithMessage("FunctionalAreaCode must contain only uppercase alphanumeric characters and underscores.");

        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Name is required.")
            .MaximumLength(200)
            .WithMessage("Name must not exceed 200 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(1000)
            .WithMessage("Description must not exceed 1000 characters.");
    }
}
