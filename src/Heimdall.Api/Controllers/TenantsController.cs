using Heimdall.Application.Tenants.Commands.CreateTenant;
using Heimdall.Application.Tenants.Commands.DeactivateTenant;
using Heimdall.Application.Tenants.Commands.UpdateTenant;
using Heimdall.Application.Tenants.Queries.GetTenant;
using Heimdall.Application.Tenants.Queries.GetTenants;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Heimdall.Api.Controllers;

[ApiController]
[Route("api/tenants")]
public class TenantsController : ControllerBase
{
    private readonly ISender _sender;

    public TenantsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost]
    [ProducesResponseType(typeof(CreateTenantResult), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateTenantCommand command, CancellationToken ct)
    {
        var result = await _sender.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.TenantId }, result);
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<GetTenantsResult>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var result = await _sender.Send(new GetTenantsQuery(), ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(GetTenantResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _sender.Send(new GetTenantQuery(id), ct);
        if (result is null)
            return NotFound();

        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(UpdateTenantResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTenantRequest request, CancellationToken ct)
    {
        var command = new UpdateTenantCommand(id, request.Name, request.Slug, request.PrimaryIdentityMode);
        var result = await _sender.Send(command, ct);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _sender.Send(new DeactivateTenantCommand(id), ct);
        return NoContent();
    }
}

public sealed record UpdateTenantRequest(
    string Name,
    string Slug,
    Heimdall.Domain.Enums.PrimaryIdentityMode PrimaryIdentityMode);
