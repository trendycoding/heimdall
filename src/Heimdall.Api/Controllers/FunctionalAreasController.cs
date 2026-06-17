using Heimdall.Application.FunctionalAreas.Commands.CreateFunctionalArea;
using Heimdall.Application.FunctionalAreas.Commands.DeactivateFunctionalArea;
using Heimdall.Application.FunctionalAreas.Commands.UpdateFunctionalArea;
using Heimdall.Application.FunctionalAreas.Queries.GetFunctionalArea;
using Heimdall.Application.FunctionalAreas.Queries.GetFunctionalAreas;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Heimdall.Api.Controllers;

[ApiController]
[Route("api/tenants/{tenantId:guid}/applications/{appId:guid}/functional-areas")]
public class FunctionalAreasController : ControllerBase
{
    private readonly ISender _sender;

    public FunctionalAreasController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost]
    [ProducesResponseType(typeof(CreateFunctionalAreaResult), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(Guid tenantId, Guid appId, [FromBody] CreateFunctionalAreaRequest request, CancellationToken ct)
    {
        var command = new CreateFunctionalAreaCommand(
            appId,
            request.FunctionalAreaCode,
            request.Name,
            request.Description);

        var result = await _sender.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { tenantId, appId, id = result.Id }, result);
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<FunctionalAreaDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(Guid tenantId, Guid appId, CancellationToken ct)
    {
        var result = await _sender.Send(new GetFunctionalAreasQuery(appId), ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(FunctionalAreaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid tenantId, Guid appId, Guid id, CancellationToken ct)
    {
        var result = await _sender.Send(new GetFunctionalAreaQuery(id), ct);
        if (result is null)
            return NotFound();

        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Update(Guid tenantId, Guid appId, Guid id, [FromBody] UpdateFunctionalAreaRequest request, CancellationToken ct)
    {
        await _sender.Send(new UpdateFunctionalAreaCommand(id, request.Name, request.Description), ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid tenantId, Guid appId, Guid id, CancellationToken ct)
    {
        await _sender.Send(new DeactivateFunctionalAreaCommand { Id = id }, ct);
        return NoContent();
    }
}

public sealed record CreateFunctionalAreaRequest(
    string FunctionalAreaCode,
    string Name,
    string? Description);

public sealed record UpdateFunctionalAreaRequest(
    string Name,
    string? Description);
