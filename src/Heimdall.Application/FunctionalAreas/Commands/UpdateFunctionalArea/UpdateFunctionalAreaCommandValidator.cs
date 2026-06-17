using FluentValidation;

namespace Heimdall.Application.FunctionalAreas.Commands.UpdateFunctionalArea;

public sealed class UpdateFunctionalAreaCommandValidator : AbstractValidator<UpdateFunctionalAreaCommand>
{
    public UpdateFunctionalAreaCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Id is required.");

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
