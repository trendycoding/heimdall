using Heimdall.Application.Permissions.Commands.CreatePermission;
using Heimdall.Application.Permissions.Commands.DeactivatePermission;
using Heimdall.Application.Permissions.Commands.UpdatePermission;
using Heimdall.Application.Permissions.Queries.GetPermission;
using Heimdall.Application.Permissions.Queries.GetPermissions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Heimdall.Api.Controllers;

[ApiController]
[Route("api/tenants/{tenantId:guid}/applications/{appId:guid}/permissions")]
public class PermissionsController : ControllerBase
{
    private readonly ISender _sender;

    public PermissionsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost]
    [ProducesResponseType(typeof(CreatePermissionResult), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(Guid tenantId, Guid appId, [FromBody] CreatePermissionRequest request, CancellationToken ct)
    {
        var command = new CreatePermissionCommand(
            appId,
            request.FunctionalAreaId,
            request.PermissionTypeId,
            request.PermissionCode,
            request.Name,
            request.Description);

        var result = await _sender.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { tenantId, appId, id = result.Id }, result);
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PermissionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(Guid tenantId, Guid appId, CancellationToken ct)
    {
        var result = await _sender.Send(new GetPermissionsQuery(appId), ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PermissionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid tenantId, Guid appId, Guid id, CancellationToken ct)
    {
        var result = await _sender.Send(new GetPermissionQuery(id), ct);
        if (result is null)
            return NotFound();

        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Update(Guid tenantId, Guid appId, Guid id, [FromBody] UpdatePermissionRequest request, CancellationToken ct)
    {
        await _sender.Send(new UpdatePermissionCommand(id, request.PermissionCode, request.Name, request.Description), ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid tenantId, Guid appId, Guid id, CancellationToken ct)
    {
        await _sender.Send(new DeactivatePermissionCommand { Id = id }, ct);
        return NoContent();
    }
}

public sealed record CreatePermissionRequest(
    Guid FunctionalAreaId,
    Guid PermissionTypeId,
    string PermissionCode,
    string Name,
    string? Description);

public sealed record UpdatePermissionRequest(
    string PermissionCode,
    string Name,
    string? Description);
