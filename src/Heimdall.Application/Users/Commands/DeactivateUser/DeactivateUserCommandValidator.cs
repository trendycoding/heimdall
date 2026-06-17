using FluentValidation;

namespace Heimdall.Application.Users.Commands.DeactivateUser;

public sealed class DeactivateUserCommandValidator : AbstractValidator<DeactivateUserCommand>
{
    public DeactivateUserCommandValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty()
            .WithMessage("TenantId is required.");

        RuleFor(x => x.UserProfileId)
            .NotEmpty()
            .WithMessage("UserProfileId is required.");
    }
}
