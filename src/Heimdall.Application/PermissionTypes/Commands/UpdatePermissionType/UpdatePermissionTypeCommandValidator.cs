using FluentValidation;

namespace Heimdall.Application.PermissionTypes.Commands.UpdatePermissionType;

public sealed class UpdatePermissionTypeCommandValidator : AbstractValidator<UpdatePermissionTypeCommand>
{
    public UpdatePermissionTypeCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Id is required.");

        RuleFor(x => x.Code)
            .NotEmpty()
            .WithMessage("Code is required.")
            .MaximumLength(100)
            .WithMessage("Code must not exceed 100 characters.")
            .Matches(@"^[A-Za-z0-9_]+$")
            .WithMessage("Code must contain only alphanumeric characters and underscores.");

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
