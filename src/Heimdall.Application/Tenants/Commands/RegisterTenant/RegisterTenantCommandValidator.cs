using FluentValidation;

namespace Heimdall.Application.Tenants.Commands.RegisterTenant;

public sealed class RegisterTenantCommandValidator : AbstractValidator<RegisterTenantCommand>
{
    public RegisterTenantCommandValidator()
    {
        RuleFor(x => x.TenantName)
            .NotEmpty().WithMessage("Tenant name is required.")
            .MaximumLength(128).WithMessage("Tenant name must not exceed 128 characters.");

        RuleFor(x => x.Slug)
            .NotEmpty().WithMessage("Slug is required.")
            .MaximumLength(64).WithMessage("Slug must not exceed 64 characters.")
            .Matches(@"^[a-z0-9][a-z0-9-]*[a-z0-9]$").WithMessage(
                "Slug must contain only lowercase alphanumeric characters and hyphens, " +
                "and must start and end with an alphanumeric character.")
            .When(x => !string.IsNullOrEmpty(x.Slug) && x.Slug.Length >= 2);

        RuleFor(x => x.Slug)
            .Matches(@"^[a-z0-9]$").WithMessage(
                "Slug must contain only lowercase alphanumeric characters.")
            .When(x => !string.IsNullOrEmpty(x.Slug) && x.Slug.Length == 1);

        RuleFor(x => x.PrimaryIdentityMode)
            .IsInEnum().WithMessage("PrimaryIdentityMode must be a valid value.");

        RuleFor(x => x.ExternalSubjectId)
            .NotEmpty().WithMessage("External subject ID is required.")
            .MaximumLength(256).WithMessage("External subject ID must not exceed 256 characters.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.")
            .MaximumLength(320).WithMessage("Email must not exceed 320 characters.");

        RuleFor(x => x.DisplayName)
            .NotEmpty().WithMessage("Display name is required.")
            .MaximumLength(256).WithMessage("Display name must not exceed 256 characters.");
    }
}
