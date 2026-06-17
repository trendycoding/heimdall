using Heimdall.Application.Common.Interfaces;
using Heimdall.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Heimdall.Application.Common.Behaviors;

/// <summary>
/// Pipeline behavior that invalidates relevant cache keys after a mutation command succeeds.
/// Only activates for commands implementing <see cref="ICacheInvalidatingCommand"/>.
/// </summary>
public sealed class CacheInvalidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ICacheService _cacheService;
    private readonly ILogger<CacheInvalidationBehavior<TRequest, TResponse>> _logger;

    public CacheInvalidationBehavior(
        ICacheService cacheService,
        ILogger<CacheInvalidationBehavior<TRequest, TResponse>> logger)
    {
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var response = await next();

        if (request is ICacheInvalidatingCommand cacheInvalidatingCommand)
        {
            var prefixes = cacheInvalidatingCommand.GetCacheKeyPrefixes();

            foreach (var prefix in prefixes)
            {
                try
                {
                    await _cacheService.RemoveByPrefixAsync(prefix, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Failed to invalidate cache for prefix '{CachePrefix}' after command {CommandName}",
                        prefix,
                        typeof(TRequest).Name);
                }
            }
        }

        return response;
    }
}
