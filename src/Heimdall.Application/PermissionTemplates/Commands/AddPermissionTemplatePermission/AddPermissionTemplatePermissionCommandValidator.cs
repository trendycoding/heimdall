using FluentValidation;

namespace Heimdall.Application.PermissionTemplates.Commands.AddPermissionTemplatePermission;

public sealed class AddPermissionTemplatePermissionCommandValidator : AbstractValidator<AddPermissionTemplatePermissionCommand>
{
    public AddPermissionTemplatePermissionCommandValidator()
    {
        RuleFor(x => x.PermissionTemplateId)
            .NotEmpty().WithMessage("PermissionTemplateId is required.");

        RuleFor(x => x.PermissionId)
            .NotEmpty().WithMessage("PermissionId is required.");

        RuleFor(x => x.Effect)
            .IsInEnum().WithMessage("Effect must be a valid value (Allow or Deny).");
    }
}
