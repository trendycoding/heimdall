using FluentValidation;

namespace Heimdall.Application.Groups.Commands.AddGroupMembership;

public sealed class AddGroupMembershipCommandValidator : AbstractValidator<AddGroupMembershipCommand>
{
    public AddGroupMembershipCommandValidator()
    {
        RuleFor(x => x.ApplicationId)
            .NotEmpty()
            .WithMessage("ApplicationId is required.");

        RuleFor(x => x.GroupId)
            .NotEmpty()
            .WithMessage("GroupId is required.");

        RuleFor(x => x.UserProfileId)
            .NotEmpty()
            .WithMessage("UserProfileId is required.");
    }
}
