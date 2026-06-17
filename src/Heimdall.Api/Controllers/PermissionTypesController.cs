using Heimdall.Application.PermissionTypes.Commands.CreatePermissionType;
using Heimdall.Application.PermissionTypes.Commands.DeactivatePermissionType;
using Heimdall.Application.PermissionTypes.Commands.UpdatePermissionType;
using Heimdall.Application.PermissionTypes.Queries.GetPermissionType;
using Heimdall.Application.PermissionTypes.Queries.GetPermissionTypes;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Heimdall.Api.Controllers;

[ApiController]
[Route("api/tenants/{tenantId:guid}/applications/{appId:guid}/permission-types")]
public class PermissionTypesController : ControllerBase
{
    private readonly ISender _sender;

    public PermissionTypesController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost]
    [ProducesResponseType(typeof(CreatePermissionTypeResult), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(Guid tenantId, Guid appId, [FromBody] CreatePermissionTypeRequest request, CancellationToken ct)
    {
        var command = new CreatePermissionTypeCommand(
            appId,
            request.Code,
            request.Name,
            request.Description,
            request.IsSystemReserved ?? false);

        var result = await _sender.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { tenantId, appId, id = result.Id }, result);
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PermissionTypeDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(Guid tenantId, Guid appId, CancellationToken ct)
    {
        var result = await _sender.Send(new GetPermissionTypesQuery(appId), ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PermissionTypeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid tenantId, Guid appId, Guid id, CancellationToken ct)
    {
        var result = await _sender.Send(new GetPermissionTypeQuery(id), ct);
        if (result is null)
            return NotFound();

        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Update(Guid tenantId, Guid appId, Guid id, [FromBody] UpdatePermissionTypeRequest request, CancellationToken ct)
    {
        await _sender.Send(new UpdatePermissionTypeCommand(id, request.Code, request.Name, request.Description), ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid tenantId, Guid appId, Guid id, CancellationToken ct)
    {
        await _sender.Send(new DeactivatePermissionTypeCommand { Id = id }, ct);
        return NoContent();
    }
}

public sealed record CreatePermissionTypeRequest(
    string Code,
    string Name,
    string? Description,
    bool? IsSystemReserved);

public sealed record UpdatePermissionTypeRequest(
    string Code,
    string Name,
    string? Description);
