using Heimdall.Application.Common.Interfaces;
using Heimdall.Domain.Entities;
using Heimdall.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Heimdall.Application.IdentityProviders.Commands.CreateIdentityProvider;

public sealed class CreateIdentityProviderCommandHandler
    : IRequestHandler<CreateIdentityProviderCommand, CreateIdentityProviderResult>
{
    private readonly IRepository<IdentityProviderConfiguration> _repository;
    private readonly IHeimdallDbContext _dbContext;

    public CreateIdentityProviderCommandHandler(
        IRepository<IdentityProviderConfiguration> repository,
        IHeimdallDbContext dbContext)
    {
        _repository = repository;
        _dbContext = dbContext;
    }

    public async Task<CreateIdentityProviderResult> Handle(
        CreateIdentityProviderCommand request,
        CancellationToken cancellationToken)
    {
        // Enforce composite uniqueness: (TenantId, ApplicationId, ProviderType, Name)
        var duplicateExists = await _dbContext.IdentityProviderConfigurations
            .AnyAsync(idp =>
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

        var entity = new IdentityProviderConfiguration
        {
            TenantId = request.TenantId,
            ApplicationId = request.ApplicationId,
            ProviderType = request.ProviderType,
            Name = request.Name,
            Issuer = request.Issuer,
            Audience = request.Audience,
            ClientId = request.ClientId,
            JwksEndpoint = request.JwksEndpoint,
            SamlMetadataUrl = request.SamlMetadataUrl,
            AllowedAlgorithms = request.AllowedAlgorithms ?? new List<string>(),
            ClaimMappings = request.ClaimMappings ?? new List<Domain.ValueObjects.ClaimMapping>(),
            ClockSkewToleranceSeconds = request.ClockSkewToleranceSeconds,
            Status = request.Status
        };

        var created = await _repository.AddAsync(entity, cancellationToken);

        return new CreateIdentityProviderResult(
            created.Id,
            created.TenantId,
            created.ApplicationId,
            created.ProviderType,
            created.Name,
            created.Issuer,
            created.Audience,
            created.ClientId,
            created.JwksEndpoint,
            created.SamlMetadataUrl,
            created.AllowedAlgorithms,
            created.ClaimMappings,
            created.ClockSkewToleranceSeconds,
            created.Status,
            created.CreatedAt,
            created.CreatedBy);
    }
}
