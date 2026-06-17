using Heimdall.Domain.Entities;
using Heimdall.Domain.Enums;
using Heimdall.Domain.Interfaces;
using MediatR;

namespace Heimdall.Application.IdentityProviders.Commands.DeactivateIdentityProvider;

public sealed class DeactivateIdentityProviderCommandHandler
    : IRequestHandler<DeactivateIdentityProviderCommand, DeactivateIdentityProviderResult>
{
    private readonly IRepository<IdentityProviderConfiguration> _repository;

    public DeactivateIdentityProviderCommandHandler(IRepository<IdentityProviderConfiguration> repository)
    {
        _repository = repository;
    }

    public async Task<DeactivateIdentityProviderResult> Handle(
        DeactivateIdentityProviderCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.ProviderId, cancellationToken);

        if (entity is null)
        {
            throw new InvalidOperationException(
                $"Identity provider configuration with Id '{request.ProviderId}' was not found.");
        }

        if (entity.Status == IdpStatus.Inactive)
        {
            throw new InvalidOperationException(
                "Identity provider configuration is already inactive.");
        }

        entity.Status = IdpStatus.Inactive;

        await _repository.UpdateAsync(entity, cancellationToken);

        return new DeactivateIdentityProviderResult(
            entity.Id,
            entity.ModifiedAt,
            entity.ModifiedBy);
    }
}
