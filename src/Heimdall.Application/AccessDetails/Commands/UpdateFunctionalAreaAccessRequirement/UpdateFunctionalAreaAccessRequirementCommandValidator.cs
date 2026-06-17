using FluentValidation;

namespace Heimdall.Application.AccessDetails.Commands.UpdateFunctionalAreaAccessRequirement;

public sealed class UpdateFunctionalAreaAccessRequirementCommandValidator
    : AbstractValidator<UpdateFunctionalAreaAccessRequirementCommand>
{
    public UpdateFunctionalAreaAccessRequirementCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Id is required.");

        RuleFor(x => x.Description)
            .MaximumLength(500)
            .WithMessage("Description must not exceed 500 characters.");
    }
}
