using FluentValidation;

namespace Heimdall.Application.PermissionTemplates.Commands.CreatePermissionTemplate;

public sealed class CreatePermissionTemplateCommandValidator : AbstractValidator<CreatePermissionTemplateCommand>
{
    public CreatePermissionTemplateCommandValidator()
    {
        RuleFor(x => x.ApplicationId)
            .NotEmpty().WithMessage("ApplicationId is required.");

        RuleFor(x => x.TemplateCode)
            .NotEmpty().WithMessage("TemplateCode is required.")
            .MaximumLength(100).WithMessage("TemplateCode must not exceed 100 characters.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(200).WithMessage("Name must not exceed 200 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Description must not exceed 1000 characters.")
            .When(x => x.Description is not null);
    }
}
