using FsCheck;
using FsCheck.Xunit;
using Heimdall.Application.Common.Behaviors;
using Heimdall.Application.Common.Interfaces;
using Heimdall.Domain.Interfaces;
using Heimdall.Infrastructure.Caching;
using Heimdall.Infrastructure.Logging;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Heimdall.Application.Tests.Properties;

/// <summary>
/// Property 18: Cache Invalidation on Mutation
/// Generate mutations (permission assignments, group changes, access detail changes);
/// assert all affected cache keys are invalidated before response.
///
/// **Validates: Requirements 22.3, 22.4, 22.5, 22.6**
/// </summary>
public class CacheInvalidation_MutationTests
{
    /// <summary>
    /// Creates an InMemoryCacheService with default configuration.
    /// </summary>
    private static InMemoryCacheService CreateCacheService()
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cache:DefaultTtlSeconds"] = "300"
            })
            .Build();

        return new InMemoryCacheService(
            memoryCache,
            configuration,
            NullLogger<InMemoryCacheService>.Instance,
            NullHeimdallMetricsService.Instance);
    }

    /// <summary>
    /// Creates a CacheInvalidationBehavior with real InMemoryCacheService.
    /// </summary>
    private static (CacheInvalidationBehavior<TRequest, TResponse> Behavior, InMemoryCacheService Cache)
        CreateBehavior<TRequest, TResponse>() where TRequest : notnull
    {
        var cacheService = CreateCacheService();
        var logger = NullLogger<CacheInvalidationBehavior<TRequest, TResponse>>.Instance;
        var behavior = new CacheInvalidationBehavior<TRequest, TResponse>(cacheService, logger);
        return (behavior, cacheService);
    }

    /// <summary>
    /// Test command implementing ICacheInvalidatingCommand with configurable prefixes.
    /// </summary>
    private sealed class TestCacheInvalidatingCommand : IRequest<MediatR.Unit>, ICacheInvalidatingCommand
    {
        public IReadOnlyList<string> Prefixes { get; init; } = [];

        public IReadOnlyList<string> GetCacheKeyPrefixes() => Prefixes;
    }

    /// <summary>
    /// Test command that does NOT implement ICacheInvalidatingCommand.
    /// </summary>
    private sealed class TestNonInvalidatingCommand : IRequest<MediatR.Unit> { }

    [Property(MaxTest = 100)]
    public bool CacheInvalidation_AllMatchingKeysRemovedAfterMutation(
        PositiveInt prefixCountRaw, PositiveInt keysPerPrefixRaw, PositiveInt unrelatedKeysRaw)
    {
        var prefixCount = (prefixCountRaw.Get % 5) + 1;       // 1-5 prefixes
        var keysPerPrefix = (keysPerPrefixRaw.Get % 5) + 1;   // 1-5 keys per prefix
        var unrelatedKeys = (unrelatedKeysRaw.Get % 10) + 1;  // 1-10 unrelated keys

        return RunCacheInvalidationTestAsync(prefixCount, keysPerPrefix, unrelatedKeys)
            .GetAwaiter().GetResult();
    }

    private static async Task<bool> RunCacheInvalidationTestAsync(
        int prefixCount, int keysPerPrefix, int unrelatedKeyCount)
    {
        var (behavior, cache) = CreateBehavior<TestCacheInvalidatingCommand, MediatR.Unit>();

        var tenantId = Guid.NewGuid();
        var applicationId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // Generate prefixes simulating real cache key patterns
        var prefixes = new List<string>();
        for (int i = 0; i < prefixCount; i++)
        {
            prefixes.Add($"perm:{tenantId}:{applicationId}:{userId}:{i}:");
        }

        // Populate cache with keys matching the prefixes
        var matchingKeys = new List<string>();
        for (int p = 0; p < prefixes.Count; p++)
        {
            for (int k = 0; k < keysPerPrefix; k++)
            {
                var key = $"{prefixes[p]}subkey-{k}";
                await cache.SetAsync(key, $"value-{p}-{k}");
                matchingKeys.Add(key);
            }
        }

        // Populate cache with unrelated keys that should NOT be invalidated
        var unrelatedKeyList = new List<string>();
        for (int i = 0; i < unrelatedKeyCount; i++)
        {
            var key = $"other:{Guid.NewGuid()}:unrelated-{i}";
            await cache.SetAsync(key, $"unrelated-value-{i}");
            unrelatedKeyList.Add(key);
        }

        // Create command with those prefixes
        var command = new TestCacheInvalidatingCommand { Prefixes = prefixes };

        // Simulate the pipeline: next() returns Unit
        RequestHandlerDelegate<MediatR.Unit> next = (ct) => Task.FromResult(MediatR.Unit.Value);

        // Act: Run the behavior
        await behavior.Handle(command, next, CancellationToken.None);

        // Assert 1: All matching keys should be removed
        foreach (var key in matchingKeys)
        {
            var cached = await cache.GetAsync<string>(key);
            if (cached != null)
                return false; // FAIL: matching key was not invalidated
        }

        // Assert 2: Unrelated keys should remain intact
        foreach (var key in unrelatedKeyList)
        {
            var cached = await cache.GetAsync<string>(key);
            if (cached == null)
                return false; // FAIL: unrelated key was incorrectly removed
        }

        return true;
    }

    [Property(MaxTest = 100)]
    public bool CacheInvalidation_PermissionAssignmentPrefixes_InvalidateCorrectKeys(
        PositiveInt extraKeysRaw)
    {
        var extraKeys = (extraKeysRaw.Get % 10) + 1;

        return RunPermissionAssignmentInvalidationTestAsync(extraKeys)
            .GetAwaiter().GetResult();
    }

    private static async Task<bool> RunPermissionAssignmentInvalidationTestAsync(int extraKeyCount)
    {
        var (behavior, cache) = CreateBehavior<TestCacheInvalidatingCommand, MediatR.Unit>();

        var tenantId = Guid.NewGuid();
        var applicationId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // Simulate the real cache key pattern for user permission assignments
        var permPrefix = $"perm:{tenantId}:{applicationId}:{userId}:";
        var effectivePrefix = $"perm-effective:{tenantId}:{applicationId}:{userId}";

        // Populate matching keys
        var permKeys = new List<string>();
        for (int i = 0; i < 3; i++)
        {
            var key = $"{permPrefix}PERM_CODE_{i}";
            await cache.SetAsync(key, "allow");
            permKeys.Add(key);
        }
        await cache.SetAsync(effectivePrefix, "effective-snapshot");

        // Populate unrelated keys (different user)
        var otherUserId = Guid.NewGuid();
        var unrelatedKeys = new List<string>();
        for (int i = 0; i < extraKeyCount; i++)
        {
            var key = $"perm:{tenantId}:{applicationId}:{otherUserId}:PERM_{i}";
            await cache.SetAsync(key, "other-user-value");
            unrelatedKeys.Add(key);
        }

        // Command with permission assignment prefixes (like CreateUserPermissionAssignmentCommand)
        var command = new TestCacheInvalidatingCommand
        {
            Prefixes = [permPrefix, effectivePrefix]
        };

        RequestHandlerDelegate<MediatR.Unit> next = (ct) => Task.FromResult(MediatR.Unit.Value);

        // Act
        await behavior.Handle(command, next, CancellationToken.None);

        // Assert: user's permission keys invalidated
        foreach (var key in permKeys)
        {
            var cached = await cache.GetAsync<string>(key);
            if (cached != null) return false;
        }

        // Assert: effective permission key invalidated
        var effectiveCached = await cache.GetAsync<string>(effectivePrefix);
        if (effectiveCached != null) return false;

        // Assert: other user's keys untouched
        foreach (var key in unrelatedKeys)
        {
            var cached = await cache.GetAsync<string>(key);
            if (cached == null) return false;
        }

        return true;
    }

    [Property(MaxTest = 100)]
    public bool CacheInvalidation_GroupMembershipChange_InvalidatesAllRelatedCaches(
        PositiveInt accessKeyCountRaw)
    {
        var accessKeyCount = (accessKeyCountRaw.Get % 5) + 1;

        return RunGroupMembershipInvalidationTestAsync(accessKeyCount)
            .GetAwaiter().GetResult();
    }

    private static async Task<bool> RunGroupMembershipInvalidationTestAsync(int accessKeyCount)
    {
        var (behavior, cache) = CreateBehavior<TestCacheInvalidatingCommand, MediatR.Unit>();

        var tenantId = Guid.NewGuid();
        var applicationId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // Simulate group membership change prefixes (like AddGroupMembershipCommand)
        var permPrefix = $"perm:{tenantId}:{applicationId}:{userId}:";
        var effectivePrefix = $"perm-effective:{tenantId}:{applicationId}:{userId}";
        var accessPrefix = $"access:{tenantId}:{applicationId}:{userId}:";
        var groupsPrefix = $"groups:{tenantId}:{applicationId}:{userId}";

        // Populate permission cache entries
        await cache.SetAsync($"{permPrefix}PERM_READ", "allow");
        await cache.SetAsync($"{permPrefix}PERM_WRITE", "deny");
        await cache.SetAsync(effectivePrefix, "effective-data");
        await cache.SetAsync(groupsPrefix, "group-list");

        // Populate access detail cache entries
        var accessKeys = new List<string>();
        for (int i = 0; i < accessKeyCount; i++)
        {
            var key = $"{accessPrefix}FA_CODE_{i}";
            await cache.SetAsync(key, $"access-detail-{i}");
            accessKeys.Add(key);
        }

        // Command with all group membership prefixes
        var command = new TestCacheInvalidatingCommand
        {
            Prefixes = [permPrefix, effectivePrefix, accessPrefix, groupsPrefix]
        };

        RequestHandlerDelegate<MediatR.Unit> next = (ct) => Task.FromResult(MediatR.Unit.Value);

        // Act
        await behavior.Handle(command, next, CancellationToken.None);

        // Assert: all matching keys invalidated
        var permRead = await cache.GetAsync<string>($"{permPrefix}PERM_READ");
        if (permRead != null) return false;

        var permWrite = await cache.GetAsync<string>($"{permPrefix}PERM_WRITE");
        if (permWrite != null) return false;

        var effective = await cache.GetAsync<string>(effectivePrefix);
        if (effective != null) return false;

        var groups = await cache.GetAsync<string>(groupsPrefix);
        if (groups != null) return false;

        foreach (var key in accessKeys)
        {
            var cached = await cache.GetAsync<string>(key);
            if (cached != null) return false;
        }

        return true;
    }

    [Property(MaxTest = 100)]
    public bool CacheInvalidation_NonInvalidatingCommand_DoesNotRemoveCache(
        PositiveInt keyCountRaw)
    {
        var keyCount = (keyCountRaw.Get % 10) + 1;

        return RunNonInvalidatingCommandTestAsync(keyCount)
            .GetAwaiter().GetResult();
    }

    private static async Task<bool> RunNonInvalidatingCommandTestAsync(int keyCount)
    {
        var (behavior, cache) = CreateBehavior<TestNonInvalidatingCommand, MediatR.Unit>();

        // Populate cache with keys
        var keys = new List<string>();
        for (int i = 0; i < keyCount; i++)
        {
            var key = $"perm:{Guid.NewGuid()}:{Guid.NewGuid()}:{Guid.NewGuid()}:PERM_{i}";
            await cache.SetAsync(key, $"value-{i}");
            keys.Add(key);
        }

        var command = new TestNonInvalidatingCommand();

        RequestHandlerDelegate<MediatR.Unit> next = (ct) => Task.FromResult(MediatR.Unit.Value);

        // Act
        await behavior.Handle(command, next, CancellationToken.None);

        // Assert: all keys remain (no invalidation for non-invalidating commands)
        foreach (var key in keys)
        {
            var cached = await cache.GetAsync<string>(key);
            if (cached == null) return false;
        }

        return true;
    }

    [Property(MaxTest = 100)]
    public bool CacheInvalidation_AccessDetailChange_InvalidatesAccessKeys(
        PositiveInt keyCountRaw, PositiveInt unrelatedCountRaw)
    {
        var keyCount = (keyCountRaw.Get % 5) + 1;
        var unrelatedCount = (unrelatedCountRaw.Get % 5) + 1;

        return RunAccessDetailInvalidationTestAsync(keyCount, unrelatedCount)
            .GetAwaiter().GetResult();
    }

    private static async Task<bool> RunAccessDetailInvalidationTestAsync(
        int matchingKeyCount, int unrelatedCount)
    {
        var (behavior, cache) = CreateBehavior<TestCacheInvalidatingCommand, MediatR.Unit>();

        var tenantId = Guid.NewGuid();
        var applicationId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // Simulate user access detail change prefix
        var accessPrefix = $"access:{tenantId}:{applicationId}:{userId}:";

        // Populate matching access keys
        var matchingKeys = new List<string>();
        for (int i = 0; i < matchingKeyCount; i++)
        {
            var key = $"{accessPrefix}FA_{i}";
            await cache.SetAsync(key, $"access-{i}");
            matchingKeys.Add(key);
        }

        // Populate unrelated keys (different user or different category)
        var unrelatedKeys = new List<string>();
        for (int i = 0; i < unrelatedCount; i++)
        {
            var key = $"access:{tenantId}:{applicationId}:{Guid.NewGuid()}:FA_{i}";
            await cache.SetAsync(key, $"other-access-{i}");
            unrelatedKeys.Add(key);
        }

        var command = new TestCacheInvalidatingCommand
        {
            Prefixes = [accessPrefix]
        };

        RequestHandlerDelegate<MediatR.Unit> next = (ct) => Task.FromResult(MediatR.Unit.Value);

        // Act
        await behavior.Handle(command, next, CancellationToken.None);

        // Assert: matching keys invalidated
        foreach (var key in matchingKeys)
        {
            var cached = await cache.GetAsync<string>(key);
            if (cached != null) return false;
        }

        // Assert: unrelated keys preserved
        foreach (var key in unrelatedKeys)
        {
            var cached = await cache.GetAsync<string>(key);
            if (cached == null) return false;
        }

        return true;
    }
}
