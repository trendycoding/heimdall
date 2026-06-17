using FluentValidation;

namespace Heimdall.Application.AccessDetails.Commands.CreateFunctionalAreaAccessRequirement;

public sealed class CreateFunctionalAreaAccessRequirementCommandValidator
    : AbstractValidator<CreateFunctionalAreaAccessRequirementCommand>
{
    public CreateFunctionalAreaAccessRequirementCommandValidator()
    {
        RuleFor(x => x.ApplicationId)
            .NotEmpty()
            .WithMessage("ApplicationId is required.");

        RuleFor(x => x.FunctionalAreaId)
            .NotEmpty()
            .WithMessage("FunctionalAreaId is required.");

        RuleFor(x => x.AccessDetailType)
            .NotEmpty()
            .WithMessage("AccessDetailType is required.");

        RuleFor(x => x.Description)
            .MaximumLength(500)
            .WithMessage("Description must not exceed 500 characters.");
    }
}
