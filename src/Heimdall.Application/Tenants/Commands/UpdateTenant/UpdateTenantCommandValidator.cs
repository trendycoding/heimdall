using FluentValidation;
using Heimdall.Domain.Enums;

namespace Heimdall.Application.Tenants.Commands.UpdateTenant;

public sealed class UpdateTenantCommandValidator : AbstractValidator<UpdateTenantCommand>
{
    public UpdateTenantCommandValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("TenantId is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(128).WithMessage("Name must not exceed 128 characters.");

        RuleFor(x => x.Slug)
            .NotEmpty().WithMessage("Slug is required.")
            .MaximumLength(64).WithMessage("Slug must not exceed 64 characters.")
            .Matches(@"^[a-z0-9][a-z0-9-]*[a-z0-9]$").WithMessage(
                "Slug must contain only lowercase alphanumeric characters and hyphens, " +
                "and must start and end with an alphanumeric character.")
            .When(x => !string.IsNullOrEmpty(x.Slug) && x.Slug.Length >= 2);

        // Handle single-character slug (valid if alphanumeric)
        RuleFor(x => x.Slug)
            .Matches(@"^[a-z0-9]$").WithMessage(
                "Slug must contain only lowercase alphanumeric characters and hyphens, " +
                "and must start and end with an alphanumeric character.")
            .When(x => !string.IsNullOrEmpty(x.Slug) && x.Slug.Length == 1);

        RuleFor(x => x.PrimaryIdentityMode)
            .IsInEnum().WithMessage("PrimaryIdentityMode must be a valid value.");
    }
}
