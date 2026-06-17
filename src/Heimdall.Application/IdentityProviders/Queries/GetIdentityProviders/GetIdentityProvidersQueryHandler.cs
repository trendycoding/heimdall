using Heimdall.Application.Common.Interfaces;
using Heimdall.Application.IdentityProviders.Queries.GetIdentityProvider;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Heimdall.Application.IdentityProviders.Queries.GetIdentityProviders;

public sealed class GetIdentityProvidersQueryHandler
    : IRequestHandler<GetIdentityProvidersQuery, IReadOnlyList<IdentityProviderDto>>
{
    private readonly IHeimdallDbContext _dbContext;

    public GetIdentityProvidersQueryHandler(IHeimdallDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<IdentityProviderDto>> Handle(
        GetIdentityProvidersQuery request,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.IdentityProviderConfigurations
            .Where(idp => idp.TenantId == request.TenantId);

        if (request.ApplicationId.HasValue)
        {
            query = query.Where(idp => idp.ApplicationId == request.ApplicationId.Value);
        }

        if (request.ProviderType.HasValue)
        {
            query = query.Where(idp => idp.ProviderType == request.ProviderType.Value);
        }

        var entities = await query
            .OrderBy(idp => idp.Name)
            .ToListAsync(cancellationToken);

        return entities.Select(entity => new IdentityProviderDto(
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
            entity.ModifiedBy))
            .ToList();
    }
}
