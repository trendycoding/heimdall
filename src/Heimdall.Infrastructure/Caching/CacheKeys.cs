namespace Heimdall.Infrastructure.Caching;

/// <summary>
/// Defines the cache key strategy for the Heimdall permission and access caching layer.
/// </summary>
public static class CacheKeys
{
    /// <summary>
    /// Cache key for a single permission check result.
    /// Pattern: perm:{tenantId}:{appId}:{userId}:{permCode}
    /// </summary>
    public static string Permission(Guid tenantId, Guid appId, Guid userId, string permissionCode)
        => $"perm:{tenantId}:{appId}:{userId}:{permissionCode}";

    /// <summary>
    /// Cache key prefix for all permission checks for a user within an app.
    /// Pattern: perm:{tenantId}:{appId}:{userId}:
    /// </summary>
    public static string PermissionPrefix(Guid tenantId, Guid appId, Guid userId)
        => $"perm:{tenantId}:{appId}:{userId}:";

    /// <summary>
    /// Cache key for effective permissions for a user within an app.
    /// Pattern: perm-effective:{tenantId}:{appId}:{userId}
    /// </summary>
    public static string EffectivePermissions(Guid tenantId, Guid appId, Guid userId)
        => $"perm-effective:{tenantId}:{appId}:{userId}";

    /// <summary>
    /// Cache key prefix for effective permissions for a user.
    /// Pattern: perm-effective:{tenantId}:{appId}:{userId}
    /// </summary>
    public static string EffectivePermissionsPrefix(Guid tenantId, Guid appId, Guid userId)
        => $"perm-effective:{tenantId}:{appId}:{userId}";

    /// <summary>
    /// Cache key for access detail lookup results.
    /// Pattern: access:{tenantId}:{appId}:{userId}:{faCode}
    /// </summary>
    public static string AccessDetails(Guid tenantId, Guid appId, Guid userId, string functionalAreaCode)
        => $"access:{tenantId}:{appId}:{userId}:{functionalAreaCode}";

    /// <summary>
    /// Cache key prefix for all access details for a user within an app.
    /// Pattern: access:{tenantId}:{appId}:{userId}:
    /// </summary>
    public static string AccessDetailsPrefix(Guid tenantId, Guid appId, Guid userId)
        => $"access:{tenantId}:{appId}:{userId}:";

    /// <summary>
    /// Cache key for group memberships for a user within an app.
    /// Pattern: groups:{tenantId}:{appId}:{userId}
    /// </summary>
    public static string Groups(Guid tenantId, Guid appId, Guid userId)
        => $"groups:{tenantId}:{appId}:{userId}";
}
