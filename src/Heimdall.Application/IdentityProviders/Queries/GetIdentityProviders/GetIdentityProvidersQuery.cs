using Heimdall.Application.IdentityProviders.Queries.GetIdentityProvider;
using Heimdall.Domain.Enums;
using MediatR;

namespace Heimdall.Application.IdentityProviders.Queries.GetIdentityProviders;

public sealed record GetIdentityProvidersQuery(
    Guid TenantId,
    Guid? ApplicationId = null,
    ProviderType? ProviderType = null) : IRequest<IReadOnlyList<IdentityProviderDto>>;
