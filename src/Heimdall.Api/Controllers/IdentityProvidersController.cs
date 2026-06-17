using Heimdall.Application.IdentityProviders.Commands.CreateIdentityProvider;
using Heimdall.Application.IdentityProviders.Commands.DeactivateIdentityProvider;
using Heimdall.Application.IdentityProviders.Commands.UpdateIdentityProvider;
using Heimdall.Application.IdentityProviders.Queries.GetIdentityProvider;
using Heimdall.Application.IdentityProviders.Queries.GetIdentityProviders;
using Heimdall.Domain.Enums;
using Heimdall.Domain.ValueObjects;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Heimdall.Api.Controllers;

[ApiController]
[Route("api/tenants/{tenantId:guid}/identity-providers")]
public class IdentityProvidersController : ControllerBase
{
    private readonly ISender _sender;

    public IdentityProvidersController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost]
    [ProducesResponseType(typeof(CreateIdentityProviderResult), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(Guid tenantId, [FromBody] CreateIdentityProviderRequest request, CancellationToken ct)
    {
        var command = new CreateIdentityProviderCommand(
            tenantId,
            request.ApplicationId,
            request.ProviderType,
            request.Name,
            request.Issuer,
            request.Audience,
            request.ClientId,
            request.JwksEndpoint,
            request.SamlMetadataUrl,
            request.AllowedAlgorithms,
            request.ClaimMappings,
            request.ClockSkewToleranceSeconds ?? 300,
            request.Status ?? IdpStatus.Active);

        var result = await _sender.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { tenantId, id = result.ProviderId }, result);
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<IdentityProviderDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(Guid tenantId, [FromQuery] Guid? applicationId, [FromQuery] ProviderType? providerType, CancellationToken ct)
    {
        var result = await _sender.Send(new GetIdentityProvidersQuery(tenantId, applicationId, providerType), ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(IdentityProviderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid tenantId, Guid id, CancellationToken ct)
    {
        var result = await _sender.Send(new GetIdentityProviderQuery(id), ct);
        if (result is null)
            return NotFound();

        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(UpdateIdentityProviderResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid tenantId, Guid id, [FromBody] UpdateIdentityProviderRequest request, CancellationToken ct)
    {
        var command = new UpdateIdentityProviderCommand(
            id,
            tenantId,
            request.ApplicationId,
            request.ProviderType,
            request.Name,
            request.Issuer,
            request.Audience,
            request.ClientId,
            request.JwksEndpoint,
            request.SamlMetadataUrl,
            request.AllowedAlgorithms,
            request.ClaimMappings,
            request.ClockSkewToleranceSeconds ?? 300);

        var result = await _sender.Send(command, ct);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid tenantId, Guid id, CancellationToken ct)
    {
        await _sender.Send(new DeactivateIdentityProviderCommand(id), ct);
        return NoContent();
    }
}

public sealed record CreateIdentityProviderRequest(
    Guid? ApplicationId,
    ProviderType ProviderType,
    string Name,
    string Issuer,
    string? Audience,
    string? ClientId,
    string? JwksEndpoint,
    string? SamlMetadataUrl,
    List<string>? AllowedAlgorithms,
    List<ClaimMapping>? ClaimMappings,
    int? ClockSkewToleranceSeconds,
    IdpStatus? Status);

public sealed record UpdateIdentityProviderRequest(
    Guid? ApplicationId,
    ProviderType ProviderType,
    string Name,
    string Issuer,
    string? Audience,
    string? ClientId,
    string? JwksEndpoint,
    string? SamlMetadataUrl,
    List<string>? AllowedAlgorithms,
    List<ClaimMapping>? ClaimMappings,
    int? ClockSkewToleranceSeconds);
