using Heimdall.Application.Applications.Commands.CreateApplication;
using Heimdall.Application.Applications.Commands.DeactivateApplication;
using Heimdall.Application.Applications.Commands.UpdateApplication;
using Heimdall.Application.Applications.Queries.GetApplication;
using Heimdall.Application.Applications.Queries.GetApplications;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Heimdall.Api.Controllers;

[ApiController]
[Route("api/tenants/{tenantId:guid}/applications")]
public class ApplicationsController : ControllerBase
{
    private readonly ISender _sender;

    public ApplicationsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost]
    [ProducesResponseType(typeof(CreateApplicationResult), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(Guid tenantId, [FromBody] CreateApplicationRequest request, CancellationToken ct)
    {
        var command = new CreateApplicationCommand(
            request.Name,
            request.ClientIdentifier,
            request.Description,
            request.AllowedRedirectUris,
            request.AllowedOrigins);

        var result = await _sender.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { tenantId, id = result.ApplicationId }, result);
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<GetApplicationsResult>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(Guid tenantId, CancellationToken ct)
    {
        var result = await _sender.Send(new GetApplicationsQuery(), ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(GetApplicationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid tenantId, Guid id, CancellationToken ct)
    {
        var result = await _sender.Send(new GetApplicationQuery(id), ct);
        if (result is null)
            return NotFound();

        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(UpdateApplicationResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid tenantId, Guid id, [FromBody] UpdateApplicationRequest request, CancellationToken ct)
    {
        var command = new UpdateApplicationCommand(
            id,
            request.Name,
            request.Description,
            request.AllowedRedirectUris,
            request.AllowedOrigins);

        var result = await _sender.Send(command, ct);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid tenantId, Guid id, CancellationToken ct)
    {
        await _sender.Send(new DeactivateApplicationCommand { ApplicationId = id }, ct);
        return NoContent();
    }
}

public sealed record CreateApplicationRequest(
    string Name,
    string ClientIdentifier,
    string? Description,
    List<string>? AllowedRedirectUris,
    List<string>? AllowedOrigins);

public sealed record UpdateApplicationRequest(
    string Name,
    string? Description,
    List<string>? AllowedRedirectUris,
    List<string>? AllowedOrigins);
