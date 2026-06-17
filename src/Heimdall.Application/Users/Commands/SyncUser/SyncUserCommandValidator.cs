using FluentValidation;

namespace Heimdall.Application.Users.Commands.SyncUser;

public sealed class SyncUserCommandValidator : AbstractValidator<SyncUserCommand>
{
    public SyncUserCommandValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty()
            .WithMessage("TenantId is required.");

        RuleFor(x => x.ExternalSubjectId)
            .NotEmpty()
            .WithMessage("ExternalSubjectId is required.")
            .MaximumLength(256)
            .WithMessage("ExternalSubjectId must not exceed 256 characters.");

        RuleFor(x => x.IdentityProvider)
            .NotEmpty()
            .WithMessage("IdentityProvider is required.")
            .MaximumLength(256)
            .WithMessage("IdentityProvider must not exceed 256 characters.");

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

        RuleForEach(x => x.Applications).ChildRules(app =>
        {
            app.RuleFor(a => a.ApplicationId)
                .NotEmpty()
                .WithMessage("ApplicationId is required in applications array.");

            app.RuleFor(a => a.PermissionTemplateCodes)
                .NotNull()
                .WithMessage("PermissionTemplateCodes must not be null.");

            app.RuleForEach(a => a.PermissionTemplateCodes)
                .NotEmpty()
                .WithMessage("PermissionTemplateCode must not be empty.");
        });
    }
}
