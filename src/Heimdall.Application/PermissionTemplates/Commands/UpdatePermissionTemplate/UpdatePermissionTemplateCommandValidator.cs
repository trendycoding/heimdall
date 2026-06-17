using FluentValidation;

namespace Heimdall.Application.PermissionTemplates.Commands.UpdatePermissionTemplate;

public sealed class UpdatePermissionTemplateCommandValidator : AbstractValidator<UpdatePermissionTemplateCommand>
{
    public UpdatePermissionTemplateCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Id is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(200).WithMessage("Name must not exceed 200 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Description must not exceed 1000 characters.")
            .When(x => x.Description is not null);
    }
}
