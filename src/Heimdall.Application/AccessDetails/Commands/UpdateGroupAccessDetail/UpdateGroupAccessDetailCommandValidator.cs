using FluentValidation;

namespace Heimdall.Application.AccessDetails.Commands.UpdateGroupAccessDetail;

public sealed class UpdateGroupAccessDetailCommandValidator : AbstractValidator<UpdateGroupAccessDetailCommand>
{
    public UpdateGroupAccessDetailCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Id is required.");

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
