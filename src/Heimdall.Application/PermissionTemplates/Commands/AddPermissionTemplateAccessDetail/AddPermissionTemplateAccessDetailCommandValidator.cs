using FluentValidation;

namespace Heimdall.Application.PermissionTemplates.Commands.AddPermissionTemplateAccessDetail;

public sealed class AddPermissionTemplateAccessDetailCommandValidator : AbstractValidator<AddPermissionTemplateAccessDetailCommand>
{
    public AddPermissionTemplateAccessDetailCommandValidator()
    {
        RuleFor(x => x.PermissionTemplateId)
            .NotEmpty().WithMessage("PermissionTemplateId is required.");

        RuleFor(x => x.AccessDetailType)
            .NotEmpty().WithMessage("AccessDetailType is required.")
            .MaximumLength(100).WithMessage("AccessDetailType must not exceed 100 characters.");

        RuleFor(x => x.AccessDetailCode)
            .NotEmpty().WithMessage("AccessDetailCode is required.")
            .MaximumLength(100).WithMessage("AccessDetailCode must not exceed 100 characters.");

        RuleFor(x => x.AccessDetailValue)
            .NotEmpty().WithMessage("AccessDetailValue is required.")
            .MaximumLength(500).WithMessage("AccessDetailValue must not exceed 500 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Description must not exceed 1000 characters.")
            .When(x => x.Description is not null);
    }
}
