using Heimdall.Application.PermissionTemplates.Commands.AddPermissionTemplateAccessDetail;
using Heimdall.Application.PermissionTemplates.Commands.AddPermissionTemplateGroup;
using Heimdall.Application.PermissionTemplates.Commands.AddPermissionTemplatePermission;
using Heimdall.Application.PermissionTemplates.Commands.CreatePermissionTemplate;
using Heimdall.Application.PermissionTemplates.Commands.DeactivatePermissionTemplate;
using Heimdall.Application.PermissionTemplates.Commands.RemovePermissionTemplateAccessDetail;
using Heimdall.Application.PermissionTemplates.Commands.RemovePermissionTemplateGroup;
using Heimdall.Application.PermissionTemplates.Commands.RemovePermissionTemplatePermission;
using Heimdall.Application.PermissionTemplates.Commands.UpdatePermissionTemplate;
using Heimdall.Application.PermissionTemplates.Queries.GetPermissionTemplate;
using Heimdall.Application.PermissionTemplates.Queries.GetPermissionTemplates;
using Heimdall.Domain.Enums;
using Heimdall.Domain.Interfaces;
using Heimdall.Domain.Models;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Heimdall.Api.Controllers;

[ApiController]
[Route("api/tenants/{tenantId:guid}/applications/{appId:guid}/permission-templates")]
public class PermissionTemplatesController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ITemplateApplicationService _templateService;

    public PermissionTemplatesController(ISender sender, ITemplateApplicationService templateService)
    {
        _sender = sender;
        _templateService = templateService;
    }

    [HttpPost]
    [ProducesResponseType(typeof(CreatePermissionTemplateResult), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(Guid tenantId, Guid appId, [FromBody] CreatePermissionTemplateRequest request, CancellationToken ct)
    {
        var command = new CreatePermissionTemplateCommand(appId, request.TemplateCode, request.Name, request.Description);
        var result = await _sender.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { tenantId, appId, id = result.PermissionTemplateId }, result);
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PermissionTemplateSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(Guid tenantId, Guid appId, CancellationToken ct)
    {
        var result = await _sender.Send(new GetPermissionTemplatesQuery(appId), ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PermissionTemplateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid tenantId, Guid appId, Guid id, CancellationToken ct)
    {
        var result = await _sender.Send(new GetPermissionTemplateQuery(id), ct);
        if (result is null)
            return NotFound();

        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Update(Guid tenantId, Guid appId, Guid id, [FromBody] UpdatePermissionTemplateRequest request, CancellationToken ct)
    {
        await _sender.Send(new UpdatePermissionTemplateCommand(id, request.Name, request.Description, request.IsActive), ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid tenantId, Guid appId, Guid id, CancellationToken ct)
    {
        await _sender.Send(new DeactivatePermissionTemplateCommand(id), ct);
        return NoContent();
    }

    // Template entries management

    [HttpPost("{templateId:guid}/permissions")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> AddPermission(Guid tenantId, Guid appId, Guid templateId, [FromBody] AddTemplatePermissionRequest request, CancellationToken ct)
    {
        var command = new AddPermissionTemplatePermissionCommand(templateId, request.PermissionId, request.Effect, request.ValidFromOffsetDays, request.ValidToOffsetDays);
        var result = await _sender.Send(command, ct);
        return Created();
    }

    [HttpDelete("{templateId:guid}/permissions/{entryId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemovePermission(Guid tenantId, Guid appId, Guid templateId, Guid entryId, CancellationToken ct)
    {
        await _sender.Send(new RemovePermissionTemplatePermissionCommand(entryId), ct);
        return NoContent();
    }

    [HttpPost("{templateId:guid}/groups")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> AddGroup(Guid tenantId, Guid appId, Guid templateId, [FromBody] AddTemplateGroupRequest request, CancellationToken ct)
    {
        var command = new AddPermissionTemplateGroupCommand(templateId, request.GroupId);
        var result = await _sender.Send(command, ct);
        return Created();
    }

    [HttpDelete("{templateId:guid}/groups/{entryId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveGroup(Guid tenantId, Guid appId, Guid templateId, Guid entryId, CancellationToken ct)
    {
        await _sender.Send(new RemovePermissionTemplateGroupCommand(entryId), ct);
        return NoContent();
    }

    [HttpPost("{templateId:guid}/access-details")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> AddAccessDetail(Guid tenantId, Guid appId, Guid templateId, [FromBody] AddTemplateAccessDetailRequest request, CancellationToken ct)
    {
        var command = new AddPermissionTemplateAccessDetailCommand(
            templateId, request.AccessDetailType, request.AccessDetailCode,
            request.AccessDetailValue, request.Description,
            request.ValidFromOffsetDays, request.ValidToOffsetDays);
        var result = await _sender.Send(command, ct);
        return Created();
    }

    [HttpDelete("{templateId:guid}/access-details/{entryId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveAccessDetail(Guid tenantId, Guid appId, Guid templateId, Guid entryId, CancellationToken ct)
    {
        await _sender.Send(new RemovePermissionTemplateAccessDetailCommand(entryId), ct);
        return NoContent();
    }

    // Apply and Preview

    [HttpPost("{templateId:guid}/apply")]
    [ProducesResponseType(typeof(TemplateApplicationResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> Apply(Guid tenantId, Guid appId, Guid templateId, [FromBody] ApplyTemplateRequest request, CancellationToken ct)
    {
        var options = new TemplateApplicationOptions
        {
            ReplaceExistingPermissions = request.ReplaceExistingPermissions,
            ReplaceExistingGroups = request.ReplaceExistingGroups,
            ReplaceExistingAccessDetails = request.ReplaceExistingAccessDetails
        };

        var result = await _templateService.ApplyTemplateAsync(tenantId, appId, request.UserProfileId, templateId, options, ct);
        return Ok(result);
    }

    [HttpPost("{templateId:guid}/preview")]
    [ProducesResponseType(typeof(TemplatePreviewResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> Preview(Guid tenantId, Guid appId, Guid templateId, [FromBody] PreviewTemplateRequest request, CancellationToken ct)
    {
        var options = new TemplateApplicationOptions
        {
            ReplaceExistingPermissions = request.ReplaceExistingPermissions,
            ReplaceExistingGroups = request.ReplaceExistingGroups,
            ReplaceExistingAccessDetails = request.ReplaceExistingAccessDetails
        };

        var result = await _templateService.PreviewTemplateAsync(tenantId, appId, request.UserProfileId, templateId, options, ct);
        return Ok(result);
    }
}

public sealed record CreatePermissionTemplateRequest(
    string TemplateCode,
    string Name,
    string? Description);

public sealed record UpdatePermissionTemplateRequest(
    string Name,
    string? Description,
    bool IsActive);

public sealed record AddTemplatePermissionRequest(
    Guid PermissionId,
    Effect Effect,
    int? ValidFromOffsetDays,
    int? ValidToOffsetDays);

public sealed record AddTemplateGroupRequest(
    Guid GroupId);

public sealed record AddTemplateAccessDetailRequest(
    string AccessDetailType,
    string AccessDetailCode,
    string AccessDetailValue,
    string? Description,
    int? ValidFromOffsetDays,
    int? ValidToOffsetDays);

public sealed record ApplyTemplateRequest(
    Guid UserProfileId,
    bool ReplaceExistingPermissions,
    bool ReplaceExistingGroups,
    bool ReplaceExistingAccessDetails);

public sealed record PreviewTemplateRequest(
    Guid UserProfileId,
    bool ReplaceExistingPermissions,
    bool ReplaceExistingGroups,
    bool ReplaceExistingAccessDetails);
