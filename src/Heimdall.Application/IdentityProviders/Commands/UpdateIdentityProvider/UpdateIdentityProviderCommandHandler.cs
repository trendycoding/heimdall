using Heimdall.Application.Common.Interfaces;
using Heimdall.Domain.Entities;
using Heimdall.Domain.Enums;
using Heimdall.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Heimdall.Application.IdentityProviders.Commands.UpdateIdentityProvider;

public sealed class UpdateIdentityProviderCommandHandler
    : IRequestHandler<UpdateIdentityProviderCommand, UpdateIdentityProviderResult>
{
    private readonly IRepository<IdentityProviderConfiguration> _repository;
    private readonly IHeimdallDbContext _dbContext;

    public UpdateIdentityProviderCommandHandler(
        IRepository<IdentityProviderConfiguration> repository,
        IHeimdallDbContext dbContext)
    {
        _repository = repository;
        _dbContext = dbContext;
    }

    public async Task<UpdateIdentityProviderResult> Handle(
        UpdateIdentityProviderCommand request,
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
                "Cannot update an inactive identity provider configuration.");
        }

        // Enforce composite uniqueness if name/type/applicationId changed
        if (entity.Name != request.Name ||
            entity.ProviderType != request.ProviderType ||
            entity.ApplicationId != request.ApplicationId)
        {
            var duplicateExists = await _dbContext.IdentityProviderConfigurations
                .AnyAsync(idp =>
                    idp.Id != request.ProviderId &&
                    idp.TenantId == request.TenantId &&
                    idp.ApplicationId == request.ApplicationId &&
                    idp.ProviderType == request.ProviderType &&
                    idp.Name == request.Name,
                    cancellationToken);

            if (duplicateExists)
            {
                throw new InvalidOperationException(
                    $"An identity provider configuration with the same TenantId, ApplicationId, ProviderType '{request.ProviderType}', and Name '{request.Name}' already exists.");
            }
        }

        // Update mutable fields
        entity.ApplicationId = request.ApplicationId;
        entity.ProviderType = request.ProviderType;
        entity.Name = request.Name;
        entity.Issuer = request.Issuer;
        entity.Audience = request.Audience;
        entity.ClientId = request.ClientId;
        entity.JwksEndpoint = request.JwksEndpoint;
        entity.SamlMetadataUrl = request.SamlMetadataUrl;
        entity.AllowedAlgorithms = request.AllowedAlgorithms ?? new List<string>();
        entity.ClaimMappings = request.ClaimMappings ?? new List<Domain.ValueObjects.ClaimMapping>();
        entity.ClockSkewToleranceSeconds = request.ClockSkewToleranceSeconds;

        await _repository.UpdateAsync(entity, cancellationToken);

        return new UpdateIdentityProviderResult(
            entity.Id,
            entity.TenantId,
            entity.ApplicationId,
            entity.ProviderType,
            entity.Name,
            entity.Issuer,
            entity.Audience,
            entity.ClientId,
            entity.JwksEndpoint,
            entity.SamlMetadataUrl,
            entity.AllowedAlgorithms,
            entity.ClaimMappings,
            entity.ClockSkewToleranceSeconds,
            entity.Status,
            entity.ModifiedAt,
            entity.ModifiedBy);
    }
}
