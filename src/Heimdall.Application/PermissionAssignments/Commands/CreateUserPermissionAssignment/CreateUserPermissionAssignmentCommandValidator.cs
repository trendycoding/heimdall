using FluentValidation;
using Heimdall.Domain.Enums;

namespace Heimdall.Application.PermissionAssignments.Commands.CreateUserPermissionAssignment;

public sealed class CreateUserPermissionAssignmentCommandValidator : AbstractValidator<CreateUserPermissionAssignmentCommand>
{
    public CreateUserPermissionAssignmentCommandValidator()
    {
        RuleFor(x => x.ApplicationId)
            .NotEmpty()
            .WithMessage("ApplicationId is required.");

        RuleFor(x => x.UserProfileId)
            .NotEmpty()
            .WithMessage("UserProfileId is required.");

        RuleFor(x => x.PermissionId)
            .NotEmpty()
            .WithMessage("PermissionId is required.");

        RuleFor(x => x.Effect)
            .IsInEnum()
            .WithMessage("Effect must be Allow or Deny.");

        RuleFor(x => x)
            .Must(x => !x.ValidFrom.HasValue || !x.ValidTo.HasValue || x.ValidFrom.Value <= x.ValidTo.Value)
            .WithMessage("ValidFrom must not be later than ValidTo.");
    }
}
