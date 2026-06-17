using FluentValidation;

namespace Heimdall.Application.PermissionTemplates.Commands.AddPermissionTemplateGroup;

public sealed class AddPermissionTemplateGroupCommandValidator : AbstractValidator<AddPermissionTemplateGroupCommand>
{
    public AddPermissionTemplateGroupCommandValidator()
    {
        RuleFor(x => x.PermissionTemplateId)
            .NotEmpty().WithMessage("PermissionTemplateId is required.");

        RuleFor(x => x.GroupId)
            .NotEmpty().WithMessage("GroupId is required.");
    }
}
