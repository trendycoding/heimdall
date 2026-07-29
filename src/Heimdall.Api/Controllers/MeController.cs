using Heimdall.Application.Common.Interfaces;
using Heimdall.Application.Tenants.Commands.RegisterTenant;
using Heimdall.Application.Tenants.Queries.GetMyTenants;
using Heimdall.Domain.Entities;
using Heimdall.Domain.Enums;
using Heimdall.Infrastructure.Identity;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Heimdall.Api.Controllers;

/// <summary>
/// Pre-tenant-scoped endpoints for the authenticated user.
/// These bypass tenant resolution since the user may not have a tenant yet (self-service onboarding).
/// </summary>
[ApiController]
[Route("api/me")]
public class MeController : ControllerBase
{
    private readonly ISender _sender;
    private readonly IHeimdallDbContext _dbContext;

    public MeController(ISender sender, IHeimdallDbContext dbContext)
    {
        _sender = sender;
        _dbContext = dbContext;
    }

    /// <summary>
    /// Returns all tenants the current user belongs to.
    /// Used by the portal after login to determine if onboarding is needed or to show a tenant selector.
    /// </summary>
    [HttpGet("tenants")]
    [ProducesResponseType(typeof(IReadOnlyList<MyTenantResult>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyTenants(CancellationToken ct)
    {
        var subjectId = GetSubjectId();
        if (subjectId is null)
            return Unauthorized();

        var result = await _sender.Send(new GetMyTenantsQuery(subjectId), ct);
        return Ok(result);
    }

    /// <summary>
    /// Self-service tenant registration.
    /// Creates a new tenant and assigns the current user as Owner.
    /// </summary>
    [HttpPost("register-tenant")]
    [ProducesResponseType(typeof(RegisterTenantResult), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RegisterTenant([FromBody] RegisterTenantRequest request, CancellationToken ct)
    {
        var subjectId = GetSubjectId();
        var email = GetEmail();

        if (subjectId is null || email is null)
            return Unauthorized();

        var command = new RegisterTenantCommand(
            TenantName: request.Name,
            Slug: request.Slug,
            PrimaryIdentityMode: request.PrimaryIdentityMode,
            ExternalSubjectId: subjectId,
            Email: email,
            DisplayName: request.DisplayName ?? email);

        try
        {
            var result = await _sender.Send(command, ct);
            return CreatedAtAction(nameof(GetMyTenants), result);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("already exists") || ex.Message.Contains("maximum number"))
        {
            return Conflict(ex.Message);
        }
    }

    /// <summary>
    /// Creates an API key for the specified tenant.
    /// Only tenant Owners can create API keys.
    /// The plain-text key is returned ONCE in the response — it cannot be retrieved again.
    /// </summary>
    [HttpPost("tenants/{tenantId:guid}/api-keys")]
    [ProducesResponseType(typeof(CreateApiKeyResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateApiKey(Guid tenantId, [FromBody] CreateApiKeyRequest request, CancellationToken ct)
    {
        var subjectId = GetSubjectId();
        var email = GetEmail();
        if (subjectId is null || email is null)
            return Unauthorized();

        // Verify the user is an Owner of this tenant
        var membership = await _dbContext.TenantMemberships
            .FirstOrDefaultAsync(m =>
                m.TenantId == tenantId
                && m.ExternalSubjectId == subjectId
                && m.Status == MembershipStatus.Active, ct);

        if (membership is null || membership.Role != TenantRole.Owner)
        {
            return Forbid();
        }

        // Generate the key
        var (plainKey, hash, prefix) = ApiKeyValidationService.GenerateKey();

        var registration = new ApiKeyRegistration
        {
            TenantId = tenantId,
            Name = request.Name,
            KeyHash = hash,
            KeyPrefix = prefix,
            Scopes = request.Scopes ?? new List<string> { nameof(AdminScope.SecurityServiceAdmin) },
            Status = ApiKeyStatus.Active,
            ExpiresAt = request.ExpiresInDays.HasValue
                ? DateTime.UtcNow.AddDays(request.ExpiresInDays.Value)
                : null,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = email
        };

        _dbContext.ApiKeyRegistrations.Add(registration);
        await _dbContext.SaveChangesAsync(ct);

        return Created($"/api/me/tenants/{tenantId}/api-keys/{registration.Id}", new CreateApiKeyResponse(
            Id: registration.Id,
            Name: registration.Name,
            Key: plainKey,
            Prefix: prefix,
            Scopes: registration.Scopes,
            ExpiresAt: registration.ExpiresAt,
            CreatedAt: registration.CreatedAt));
    }

    /// <summary>
    /// Lists API keys for the specified tenant (without exposing the actual key values).
    /// </summary>
    [HttpGet("tenants/{tenantId:guid}/api-keys")]
    [ProducesResponseType(typeof(IReadOnlyList<ApiKeyListItem>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListApiKeys(Guid tenantId, CancellationToken ct)
    {
        var subjectId = GetSubjectId();
        if (subjectId is null)
            return Unauthorized();

        // Verify the user has membership in this tenant
        var membership = await _dbContext.TenantMemberships
            .FirstOrDefaultAsync(m =>
                m.TenantId == tenantId
                && m.ExternalSubjectId == subjectId
                && m.Status == MembershipStatus.Active, ct);

        if (membership is null)
            return Forbid();

        var keys = await _dbContext.ApiKeyRegistrations
            .Where(k => k.TenantId == tenantId)
            .OrderByDescending(k => k.CreatedAt)
            .Select(k => new ApiKeyListItem(
                k.Id, k.Name, k.KeyPrefix, k.Scopes, k.Status.ToString(),
                k.ExpiresAt, k.LastUsedAt, k.CreatedAt, k.CreatedBy))
            .ToListAsync(ct);

        return Ok(keys);
    }

    /// <summary>
    /// Revokes an API key.
    /// </summary>
    [HttpDelete("tenants/{tenantId:guid}/api-keys/{keyId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RevokeApiKey(Guid tenantId, Guid keyId, CancellationToken ct)
    {
        var subjectId = GetSubjectId();
        if (subjectId is null)
            return Unauthorized();

        var membership = await _dbContext.TenantMemberships
            .FirstOrDefaultAsync(m =>
                m.TenantId == tenantId
                && m.ExternalSubjectId == subjectId
                && m.Role == TenantRole.Owner
                && m.Status == MembershipStatus.Active, ct);

        if (membership is null)
            return Forbid();

        var key = await _dbContext.ApiKeyRegistrations
            .FirstOrDefaultAsync(k => k.Id == keyId && k.TenantId == tenantId, ct);

        if (key is null)
            return NotFound();

        key.Status = ApiKeyStatus.Revoked;
        await _dbContext.SaveChangesAsync(ct);

        return NoContent();
    }

    private string? GetSubjectId()
    {
        return HttpContext.User?.FindFirst("sub")?.Value
            ?? HttpContext.User?.FindFirst("oid")?.Value;
    }

    private string? GetEmail()
    {
        return HttpContext.User?.FindFirst("email")?.Value
            ?? HttpContext.User?.FindFirst("preferred_username")?.Value
            ?? HttpContext.User?.FindFirst("upn")?.Value;
    }
}

/// <summary>
/// Request body for self-service tenant registration.
/// </summary>
public sealed record RegisterTenantRequest(
    string Name,
    string Slug,
    PrimaryIdentityMode PrimaryIdentityMode,
    string? DisplayName);

public sealed record CreateApiKeyRequest(
    string Name,
    List<string>? Scopes,
    int? ExpiresInDays);

public sealed record CreateApiKeyResponse(
    Guid Id,
    string Name,
    string Key,
    string Prefix,
    List<string> Scopes,
    DateTime? ExpiresAt,
    DateTime CreatedAt);

public sealed record ApiKeyListItem(
    Guid Id,
    string Name,
    string Prefix,
    List<string> Scopes,
    string Status,
    DateTime? ExpiresAt,
    DateTime? LastUsedAt,
    DateTime CreatedAt,
    string CreatedBy);
