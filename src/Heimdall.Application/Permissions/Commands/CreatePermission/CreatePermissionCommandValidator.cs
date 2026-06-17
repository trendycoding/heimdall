using FluentValidation;

namespace Heimdall.Application.Permissions.Commands.CreatePermission;

public sealed class CreatePermissionCommandValidator : AbstractValidator<CreatePermissionCommand>
{
    public CreatePermissionCommandValidator()
    {
        RuleFor(x => x.ApplicationId)
            .NotEmpty()
            .WithMessage("ApplicationId is required.");

        RuleFor(x => x.FunctionalAreaId)
            .NotEmpty()
            .WithMessage("FunctionalAreaId is required.");

        RuleFor(x => x.PermissionTypeId)
            .NotEmpty()
            .WithMessage("PermissionTypeId is required.");

        RuleFor(x => x.PermissionCode)
            .NotEmpty()
            .WithMessage("PermissionCode is required.")
            .MaximumLength(200)
            .WithMessage("PermissionCode must not exceed 200 characters.")
            .Matches(@"^[A-Z0-9_]+$")
            .WithMessage("PermissionCode must contain only uppercase alphanumeric characters and underscores.");

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
