using Heimdall.Domain.Entities;
using Heimdall.Domain.Interfaces;
using MediatR;

namespace Heimdall.Application.IdentityProviders.Queries.GetIdentityProvider;

public sealed class GetIdentityProviderQueryHandler
    : IRequestHandler<GetIdentityProviderQuery, IdentityProviderDto?>
{
    private readonly IRepository<IdentityProviderConfiguration> _repository;

    public GetIdentityProviderQueryHandler(IRepository<IdentityProviderConfiguration> repository)
    {
        _repository = repository;
    }

    public async Task<IdentityProviderDto?> Handle(
        GetIdentityProviderQuery request,
        CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.ProviderId, cancellationToken);

        if (entity is null)
        {
            return null;
        }

        return new IdentityProviderDto(
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
            entity.CreatedAt,
            entity.CreatedBy,
            entity.ModifiedAt,
            entity.ModifiedBy);
    }
}
