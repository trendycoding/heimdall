namespace Heimdall.Application.Common.Interfaces;

/// <summary>
/// Marker interface for commands that should invalidate cache entries after successful execution.
/// </summary>
public interface ICacheInvalidatingCommand
{
    /// <summary>
    /// Returns the cache key prefixes that should be invalidated after this command succeeds.
    /// </summary>
    IReadOnlyList<string> GetCacheKeyPrefixes();
}
