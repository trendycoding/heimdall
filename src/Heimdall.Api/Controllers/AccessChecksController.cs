using Heimdall.Domain.Interfaces;
using Heimdall.Domain.Models;
using Microsoft.AspNetCore.Mvc;

namespace Heimdall.Api.Controllers;

/// <summary>
/// Handles runtime permission checks and effective permission retrieval.
/// Uses IPermissionResolver directly (not MediatR) for performance reasons.
/// </summary>
[ApiController]
[Route("api/tenants/{tenantId}/applications/{appId}/access-checks")]
public sealed class AccessChecksController : ControllerBase
{
    private readonly IPermissionResolver _permissionResolver;

    public AccessChecksController(IPermissionResolver permissionResolver)
    {
        _permissionResolver = permissionResolver;
    }

    /// <summary>
    /// Perform a single permission check by FA+PT code, PermissionCode, or ExternalSubjectId.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(PermissionCheckResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CheckPermission(
        [FromRoute] Guid tenantId,
        [FromRoute] Guid appId,
        [FromBody] SinglePermissionCheckRequest request,
        CancellationToken ct)
    {
        if (request.UserProfileId == Guid.Empty
            && string.IsNullOrWhiteSpace(request.ExternalSubjectId))
        {
            return BadRequest("Either UserProfileId or ExternalSubjectId must be provided.");
        }

        PermissionCheckResult result;

        // ExternalSubjectId-based check
        if (!string.IsNullOrWhiteSpace(request.ExternalSubjectId)
            && !string.IsNullOrWhiteSpace(request.IdentityProvider))
        {
            var permissionCode = request.PermissionCode;
            if (string.IsNullOrWhiteSpace(permissionCode))
            {
                // Build permission code from FA + PT if not provided directly
                if (string.IsNullOrWhiteSpace(request.FunctionalAreaCode)
                    || string.IsNullOrWhiteSpace(request.PermissionTypeCode))
                {
                    return BadRequest("Either PermissionCode or both FunctionalAreaCode and PermissionTypeCode must be provided.");
                }

                permissionCode = $"{request.FunctionalAreaCode}_{request.PermissionTypeCode}";
            }

            result = await _permissionResolver.CheckPermissionByExternalIdAsync(
                tenantId, appId,
                request.ExternalSubjectId, request.IdentityProvider,
                permissionCode, null, ct);
        }
        // PermissionCode-based check
        else if (!string.IsNullOrWhiteSpace(request.PermissionCode))
        {
            result = await _permissionResolver.CheckPermissionByCodeAsync(
                tenantId, appId, request.UserProfileId,
                request.PermissionCode, null, ct);
        }
        // FunctionalAreaCode + PermissionTypeCode check
        else if (!string.IsNullOrWhiteSpace(request.FunctionalAreaCode)
                 && !string.IsNullOrWhiteSpace(request.PermissionTypeCode))
        {
            result = await _permissionResolver.CheckPermissionAsync(
                tenantId, appId, request.UserProfileId,
                request.FunctionalAreaCode, request.PermissionTypeCode, null, ct);
        }
        else
        {
            return BadRequest("Either PermissionCode or both FunctionalAreaCode and PermissionTypeCode must be provided.");
        }

        return Ok(result);
    }

    /// <summary>
    /// Perform a batch permission check (max 50 items).
    /// </summary>
    [HttpPost("batch")]
    [ProducesResponseType(typeof(BatchPermissionCheckResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CheckBatch(
        [FromRoute] Guid tenantId,
        [FromRoute] Guid appId,
        [FromBody] BatchPermissionCheckApiRequest request,
        CancellationToken ct)
    {
        if (request.UserProfileId == Guid.Empty)
        {
            return BadRequest("UserProfileId is required.");
        }

        if (request.Checks is null || request.Checks.Count == 0)
        {
            return BadRequest("At least one permission check is required.");
        }

        if (request.Checks.Count > 50)
        {
            return BadRequest("Batch permission check is limited to a maximum of 50 items.");
        }

        var checks = request.Checks.Select(c => new PermissionCheckRequest
        {
            FunctionalAreaCode = c.FunctionalAreaCode,
            PermissionTypeCode = c.PermissionTypeCode,
            PermissionCode = c.PermissionCode
        }).ToList();

        var result = await _permissionResolver.CheckBatchAsync(
            tenantId, appId, request.UserProfileId, checks, null, ct);

        return Ok(result);
    }

    /// <summary>
    /// Get all effective permissions for a user within an application.
    /// </summary>
    [HttpGet("effective/{userProfileId:guid}")]
    [ProducesResponseType(typeof(EffectivePermissionsResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEffectivePermissions(
        [FromRoute] Guid tenantId,
        [FromRoute] Guid appId,
        [FromRoute] Guid userProfileId,
        CancellationToken ct)
    {
        var result = await _permissionResolver.GetEffectivePermissionsAsync(
            tenantId, appId, userProfileId, null, ct);

        return Ok(result);
    }
}

/// <summary>
/// Request body for a single permission check.
/// </summary>
public sealed class SinglePermissionCheckRequest
{
    public Guid UserProfileId { get; init; }
    public string? FunctionalAreaCode { get; init; }
    public string? PermissionTypeCode { get; init; }
    public string? PermissionCode { get; init; }
    public string? ExternalSubjectId { get; init; }
    public string? IdentityProvider { get; init; }
}

/// <summary>
/// Request body for a batch permission check.
/// </summary>
public sealed class BatchPermissionCheckApiRequest
{
    public Guid UserProfileId { get; init; }
    public List<BatchPermissionCheckItem> Checks { get; init; } = [];
}

/// <summary>
/// A single item in a batch permission check request.
/// </summary>
public sealed class BatchPermissionCheckItem
{
    public string? FunctionalAreaCode { get; init; }
    public string? PermissionTypeCode { get; init; }
    public string? PermissionCode { get; init; }
}
