using FluentValidation;

namespace Heimdall.Application.Users.Commands.UpdateUser;

public sealed class UpdateUserCommandValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserCommandValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty()
            .WithMessage("TenantId is required.");

        RuleFor(x => x.UserProfileId)
            .NotEmpty()
            .WithMessage("UserProfileId is required.");

        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage("Email is required.")
            .MaximumLength(320)
            .WithMessage("Email must not exceed 320 characters.")
            .EmailAddress()
            .WithMessage("Email must be a valid email address.");

        RuleFor(x => x.DisplayName)
            .MaximumLength(256)
            .WithMessage("DisplayName must not exceed 256 characters.");
    }
}
