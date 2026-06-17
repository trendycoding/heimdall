using FluentValidation;

namespace Heimdall.Application.AccessDetails.Commands.CreateUserAccessDetail;

public sealed class CreateUserAccessDetailCommandValidator : AbstractValidator<CreateUserAccessDetailCommand>
{
    public CreateUserAccessDetailCommandValidator()
    {
        RuleFor(x => x.ApplicationId)
            .NotEmpty()
            .WithMessage("ApplicationId is required.");

        RuleFor(x => x.UserProfileId)
            .NotEmpty()
            .WithMessage("UserProfileId is required.");

        RuleFor(x => x.AccessDetailType)
            .NotEmpty()
            .WithMessage("AccessDetailType is required.");

        RuleFor(x => x.AccessDetailCode)
            .NotEmpty()
            .WithMessage("AccessDetailCode is required.");

        RuleFor(x => x.AccessDetailValue)
            .NotEmpty()
            .WithMessage("AccessDetailValue is required.")
            .MaximumLength(500)
            .WithMessage("AccessDetailValue must not exceed 500 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(1000)
            .WithMessage("Description must not exceed 1000 characters.");

        RuleFor(x => x.ValidFrom)
            .LessThanOrEqualTo(x => x.ValidTo)
            .When(x => x.ValidFrom.HasValue && x.ValidTo.HasValue)
            .WithMessage("ValidFrom must not be later than ValidTo.");
    }
}
