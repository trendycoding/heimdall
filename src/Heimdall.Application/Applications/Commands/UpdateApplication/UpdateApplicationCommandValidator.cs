using FluentValidation;

namespace Heimdall.Application.Applications.Commands.UpdateApplication;

public sealed class UpdateApplicationCommandValidator : AbstractValidator<UpdateApplicationCommand>
{
    public UpdateApplicationCommandValidator()
    {
        RuleFor(x => x.ApplicationId)
            .NotEmpty().WithMessage("ApplicationId is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(200).WithMessage("Name must not exceed 200 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Description must not exceed 1000 characters.")
            .When(x => x.Description is not null);

        RuleFor(x => x.AllowedRedirectUris)
            .Must(uris => uris!.Count <= 20)
            .WithMessage("AllowedRedirectUris must not contain more than 20 entries.")
            .When(x => x.AllowedRedirectUris is not null);

        RuleForEach(x => x.AllowedRedirectUris)
            .Must(BeAValidAbsoluteUri)
            .WithMessage("Each AllowedRedirectUri must be a valid absolute URI.")
            .When(x => x.AllowedRedirectUris is not null);

        RuleFor(x => x.AllowedOrigins)
            .Must(origins => origins!.Count <= 20)
            .WithMessage("AllowedOrigins must not contain more than 20 entries.")
            .When(x => x.AllowedOrigins is not null);
    }

    private static bool BeAValidAbsoluteUri(string uri)
    {
        return Uri.TryCreate(uri, UriKind.Absolute, out _);
    }
}
