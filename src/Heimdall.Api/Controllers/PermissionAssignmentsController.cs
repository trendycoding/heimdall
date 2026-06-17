using Heimdall.Application.PermissionAssignments.Commands.CreateGroupPermissionAssignment;
using Heimdall.Application.PermissionAssignments.Commands.CreateUserPermissionAssignment;
using Heimdall.Application.PermissionAssignments.Commands.DeleteGroupPermissionAssignment;
using Heimdall.Application.PermissionAssignments.Commands.DeleteUserPermissionAssignment;
using Heimdall.Application.PermissionAssignments.Queries.GetGroupPermissionAssignments;
using Heimdall.Application.PermissionAssignments.Queries.GetUserPermissionAssignments;
using Heimdall.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Heimdall.Api.Controllers;

[ApiController]
[Route("api/tenants/{tenantId:guid}/applications/{appId:guid}/permission-assignments")]
public class PermissionAssignmentsController : ControllerBase
{
    private readonly ISender _sender;

    public PermissionAssignmentsController(ISender sender)
    {
        _sender = sender;
    }

    // User permission assignments

    [HttpPost("users")]
    [ProducesResponseType(typeof(CreateUserPermissionAssignmentResult), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateUserAssignment(Guid tenantId, Guid appId, [FromBody] CreateUserPermissionAssignmentRequest request, CancellationToken ct)
    {
        var command = new CreateUserPermissionAssignmentCommand
        {
            TenantId = tenantId,
            ApplicationId = appId,
            UserProfileId = request.UserProfileId,
            PermissionId = request.PermissionId,
            Effect = request.Effect,
            ValidFrom = request.ValidFrom,
            ValidTo = request.ValidTo
        };

        var result = await _sender.Send(command, ct);
        return CreatedAtAction(nameof(GetUserAssignments), new { tenantId, appId, userProfileId = request.UserProfileId }, result);
    }

    [HttpGet("users/{userProfileId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<UserPermissionAssignmentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserAssignments(Guid tenantId, Guid appId, Guid userProfileId, CancellationToken ct)
    {
        var result = await _sender.Send(new GetUserPermissionAssignmentsQuery(userProfileId, appId), ct);
        return Ok(result);
    }

    [HttpDelete("users/{assignmentId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteUserAssignment(Guid tenantId, Guid appId, Guid assignmentId, CancellationToken ct)
    {
        await _sender.Send(new DeleteUserPermissionAssignmentCommand { Id = assignmentId }, ct);
        return NoContent();
    }

    // Group permission assignments

    [HttpPost("groups")]
    [ProducesResponseType(typeof(CreateGroupPermissionAssignmentResult), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateGroupAssignment(Guid tenantId, Guid appId, [FromBody] CreateGroupPermissionAssignmentRequest request, CancellationToken ct)
    {
        var command = new CreateGroupPermissionAssignmentCommand
        {
            TenantId = tenantId,
            ApplicationId = appId,
            GroupId = request.GroupId,
            PermissionId = request.PermissionId,
            Effect = request.Effect,
            ValidFrom = request.ValidFrom,
            ValidTo = request.ValidTo
        };

        var result = await _sender.Send(command, ct);
        return CreatedAtAction(nameof(GetGroupAssignments), new { tenantId, appId, groupId = request.GroupId }, result);
    }

    [HttpGet("groups/{groupId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<GroupPermissionAssignmentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetGroupAssignments(Guid tenantId, Guid appId, Guid groupId, CancellationToken ct)
    {
        var result = await _sender.Send(new GetGroupPermissionAssignmentsQuery(groupId, appId), ct);
        return Ok(result);
    }

    [HttpDelete("groups/{assignmentId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteGroupAssignment(Guid tenantId, Guid appId, Guid assignmentId, CancellationToken ct)
    {
        await _sender.Send(new DeleteGroupPermissionAssignmentCommand { Id = assignmentId }, ct);
        return NoContent();
    }
}

public sealed record CreateUserPermissionAssignmentRequest(
    Guid UserProfileId,
    Guid PermissionId,
    Effect Effect,
    DateTime? ValidFrom,
    DateTime? ValidTo);

public sealed record CreateGroupPermissionAssignmentRequest(
    Guid GroupId,
    Guid PermissionId,
    Effect Effect,
    DateTime? ValidFrom,
    DateTime? ValidTo);
