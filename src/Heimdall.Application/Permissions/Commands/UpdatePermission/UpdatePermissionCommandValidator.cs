using FluentValidation;

namespace Heimdall.Application.Permissions.Commands.UpdatePermission;

public sealed class UpdatePermissionCommandValidator : AbstractValidator<UpdatePermissionCommand>
{
    public UpdatePermissionCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Id is required.");

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
