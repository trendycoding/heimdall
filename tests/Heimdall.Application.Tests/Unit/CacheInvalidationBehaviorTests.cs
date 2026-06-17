using Heimdall.Application.Common.Behaviors;
using Heimdall.Application.Common.Interfaces;
using Heimdall.Application.AccessDetails.Commands.CreateUserAccessDetail;
using Heimdall.Application.Groups.Commands.AddGroupMembership;
using Heimdall.Application.PermissionAssignments.Commands.CreateGroupPermissionAssignment;
using Heimdall.Application.PermissionAssignments.Commands.CreateUserPermissionAssignment;
using Heimdall.Domain.Enums;
using Heimdall.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Heimdall.Application.Tests.Unit;

/// <summary>
/// Unit tests for cache invalidation behavior covering Requirement 27.4:
/// - Permission assignment change invalidates affected user cache
/// - Group permission assignment change invalidates all group members' cache
/// - Group membership change invalidates affected user's permissions and access details cache
/// - Access detail change invalidates affected user cache
/// </summary>
public class CacheInvalidationBehaviorTests
{
    private readonly ICacheService _cacheService;

    public CacheInvalidationBehaviorTests()
    {
        _cacheService = Substitute.For<ICacheService>();
    }

    [Fact]
    public async Task UserPermissionAssignment_Invalidates_AffectedUserCache()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var appId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var permissionId = Guid.NewGuid();

        var command = new CreateUserPermissionAssignmentCommand
        {
            TenantId = tenantId,
            ApplicationId = appId,
            UserProfileId = userId,
            PermissionId = permissionId,
            Effect = Effect.Allow
        };

        var expectedResult = new CreateUserPermissionAssignmentResult(Guid.NewGuid());
        var behavior = CreateBehavior<CreateUserPermissionAssignmentCommand, CreateUserPermissionAssignmentResult>();

        RequestHandlerDelegate<CreateUserPermissionAssignmentResult> next =
            (ct) => Task.FromResult(expectedResult);

        // Act
        var result = await behavior.Handle(command, next, CancellationToken.None);

        // Assert
        Assert.Equal(expectedResult, result);

        // Verify user-specific permission cache is invalidated
        await _cacheService.Received().RemoveByPrefixAsync(
            $"perm:{tenantId}:{appId}:{userId}:",
            Arg.Any<CancellationToken>());

        // Verify user-specific effective permission cache is invalidated
        await _cacheService.Received().RemoveByPrefixAsync(
            $"perm-effective:{tenantId}:{appId}:{userId}",
            Arg.Any<CancellationToken>());

        // Verify no broader invalidation happened (exactly 2 calls)
        await _cacheService.Received(2).RemoveByPrefixAsync(
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GroupPermissionAssignment_Invalidates_AllGroupMembersCache()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var appId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var permissionId = Guid.NewGuid();

        var command = new CreateGroupPermissionAssignmentCommand
        {
            TenantId = tenantId,
            ApplicationId = appId,
            GroupId = groupId,
            PermissionId = permissionId,
            Effect = Effect.Allow
        };

        var expectedResult = new CreateGroupPermissionAssignmentResult(Guid.NewGuid());
        var behavior = CreateBehavior<CreateGroupPermissionAssignmentCommand, CreateGroupPermissionAssignmentResult>();

        RequestHandlerDelegate<CreateGroupPermissionAssignmentResult> next =
            (ct) => Task.FromResult(expectedResult);

        // Act
        var result = await behavior.Handle(command, next, CancellationToken.None);

        // Assert
        Assert.Equal(expectedResult, result);

        // Group permission changes use application-wide prefix to invalidate all group members
        await _cacheService.Received().RemoveByPrefixAsync(
            $"perm:{tenantId}:{appId}:",
            Arg.Any<CancellationToken>());

        await _cacheService.Received().RemoveByPrefixAsync(
            $"perm-effective:{tenantId}:{appId}:",
            Arg.Any<CancellationToken>());

        await _cacheService.Received(2).RemoveByPrefixAsync(
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GroupMembershipChange_Invalidates_UserPermissionsAndAccessDetailsCache()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var appId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var command = new AddGroupMembershipCommand
        {
            TenantId = tenantId,
            ApplicationId = appId,
            GroupId = groupId,
            UserProfileId = userId
        };

        var expectedResult = new AddGroupMembershipResult(Guid.NewGuid());
        var behavior = CreateBehavior<AddGroupMembershipCommand, AddGroupMembershipResult>();

        RequestHandlerDelegate<AddGroupMembershipResult> next =
            (ct) => Task.FromResult(expectedResult);

        // Act
        var result = await behavior.Handle(command, next, CancellationToken.None);

        // Assert
        Assert.Equal(expectedResult, result);

        // Verify permission cache invalidated
        await _cacheService.Received().RemoveByPrefixAsync(
            $"perm:{tenantId}:{appId}:{userId}:",
            Arg.Any<CancellationToken>());

        // Verify effective permission cache invalidated
        await _cacheService.Received().RemoveByPrefixAsync(
            $"perm-effective:{tenantId}:{appId}:{userId}",
            Arg.Any<CancellationToken>());

        // Verify access detail cache invalidated
        await _cacheService.Received().RemoveByPrefixAsync(
            $"access:{tenantId}:{appId}:{userId}:",
            Arg.Any<CancellationToken>());

        // Verify groups cache invalidated
        await _cacheService.Received().RemoveByPrefixAsync(
            $"groups:{tenantId}:{appId}:{userId}",
            Arg.Any<CancellationToken>());

        // Total of 4 invalidation calls
        await _cacheService.Received(4).RemoveByPrefixAsync(
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AccessDetailChange_Invalidates_AffectedUserCache()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var appId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var command = new CreateUserAccessDetailCommand
        {
            TenantId = tenantId,
            ApplicationId = appId,
            UserProfileId = userId,
            AccessDetailType = "REGION",
            AccessDetailCode = "US_EAST",
            AccessDetailValue = "East Region"
        };

        var expectedResult = new CreateUserAccessDetailResult(Guid.NewGuid());
        var behavior = CreateBehavior<CreateUserAccessDetailCommand, CreateUserAccessDetailResult>();

        RequestHandlerDelegate<CreateUserAccessDetailResult> next =
            (ct) => Task.FromResult(expectedResult);

        // Act
        var result = await behavior.Handle(command, next, CancellationToken.None);

        // Assert
        Assert.Equal(expectedResult, result);

        // Verify user's access detail cache is invalidated
        await _cacheService.Received().RemoveByPrefixAsync(
            $"access:{tenantId}:{appId}:{userId}:",
            Arg.Any<CancellationToken>());

        // Only access cache invalidated (1 call)
        await _cacheService.Received(1).RemoveByPrefixAsync(
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NonCacheInvalidatingCommand_DoesNotInvalidateCache()
    {
        // Arrange
        var command = new NonCacheInvalidatingRequest();
        var behavior = CreateBehavior<NonCacheInvalidatingRequest, MediatR.Unit>();

        RequestHandlerDelegate<MediatR.Unit> next =
            (ct) => Task.FromResult(MediatR.Unit.Value);

        // Act
        await behavior.Handle(command, next, CancellationToken.None);

        // Assert — no cache invalidation should occur
        await _cacheService.DidNotReceive().RemoveByPrefixAsync(
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CacheInvalidationFailure_DoesNotPreventResponse()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var appId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var command = new CreateUserAccessDetailCommand
        {
            TenantId = tenantId,
            ApplicationId = appId,
            UserProfileId = userId,
            AccessDetailType = "REGION",
            AccessDetailCode = "US_EAST",
            AccessDetailValue = "East Region"
        };

        var expectedResult = new CreateUserAccessDetailResult(Guid.NewGuid());

        _cacheService.RemoveByPrefixAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("Redis unavailable")));

        var behavior = CreateBehavior<CreateUserAccessDetailCommand, CreateUserAccessDetailResult>();

        RequestHandlerDelegate<CreateUserAccessDetailResult> next =
            (ct) => Task.FromResult(expectedResult);

        // Act — should not throw despite cache failure
        var result = await behavior.Handle(command, next, CancellationToken.None);

        // Assert — the command result is still returned
        Assert.Equal(expectedResult, result);
    }

    private CacheInvalidationBehavior<TRequest, TResponse> CreateBehavior<TRequest, TResponse>()
        where TRequest : notnull
    {
        var logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<CacheInvalidationBehavior<TRequest, TResponse>>();
        return new CacheInvalidationBehavior<TRequest, TResponse>(_cacheService, logger);
    }

    /// <summary>
    /// A simple request that does NOT implement ICacheInvalidatingCommand.
    /// </summary>
    private sealed class NonCacheInvalidatingRequest : IRequest<MediatR.Unit> { }
}
