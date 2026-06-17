using Heimdall.Application.Groups.Commands.AddGroupMembership;
using Heimdall.Application.Groups.Commands.CreateGroup;
using Heimdall.Application.Groups.Commands.DeactivateGroup;
using Heimdall.Application.Groups.Commands.RemoveGroupMembership;
using Heimdall.Application.Groups.Queries.GetGroup;
using Heimdall.Application.Groups.Queries.GetGroupMembers;
using Heimdall.Application.Groups.Queries.GetGroups;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Heimdall.Api.Controllers;

[ApiController]
[Route("api/tenants/{tenantId:guid}/applications/{appId:guid}/groups")]
public class GroupsController : ControllerBase
{
    private readonly ISender _sender;

    public GroupsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost]
    [ProducesResponseType(typeof(CreateGroupResult), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(Guid tenantId, Guid appId, [FromBody] CreateGroupRequest request, CancellationToken ct)
    {
        var command = new CreateGroupCommand(appId, request.Name, request.Description);
        var result = await _sender.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { tenantId, appId, id = result.Id }, result);
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<GroupDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(Guid tenantId, Guid appId, CancellationToken ct)
    {
        var result = await _sender.Send(new GetGroupsQuery(appId), ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(GroupDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid tenantId, Guid appId, Guid id, CancellationToken ct)
    {
        var result = await _sender.Send(new GetGroupQuery(id), ct);
        if (result is null)
            return NotFound();

        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Update(Guid tenantId, Guid appId, Guid id, CancellationToken ct)
    {
        // Group update would be handled by a dedicated UpdateGroupCommand if needed
        // Currently the design only supports deactivation for group updates
        await _sender.Send(new DeactivateGroupCommand { Id = id }, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid tenantId, Guid appId, Guid id, CancellationToken ct)
    {
        await _sender.Send(new DeactivateGroupCommand { Id = id }, ct);
        return NoContent();
    }

    // Membership endpoints

    [HttpGet("{groupId:guid}/members")]
    [ProducesResponseType(typeof(IReadOnlyList<GroupMemberDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMembers(Guid tenantId, Guid appId, Guid groupId, CancellationToken ct)
    {
        var result = await _sender.Send(new GetGroupMembersQuery(groupId), ct);
        return Ok(result);
    }

    [HttpPost("{groupId:guid}/members")]
    [ProducesResponseType(typeof(AddGroupMembershipResult), StatusCodes.Status201Created)]
    public async Task<IActionResult> AddMember(Guid tenantId, Guid appId, Guid groupId, [FromBody] AddGroupMemberRequest request, CancellationToken ct)
    {
        var command = new AddGroupMembershipCommand
        {
            TenantId = tenantId,
            ApplicationId = appId,
            GroupId = groupId,
            UserProfileId = request.UserProfileId
        };

        var result = await _sender.Send(command, ct);
        return CreatedAtAction(nameof(GetMembers), new { tenantId, appId, groupId }, result);
    }

    [HttpDelete("{groupId:guid}/members/{membershipId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveMember(Guid tenantId, Guid appId, Guid groupId, Guid membershipId, CancellationToken ct)
    {
        await _sender.Send(new RemoveGroupMembershipCommand { Id = membershipId }, ct);
        return NoContent();
    }
}

public sealed record CreateGroupRequest(
    string Name,
    string? Description);

public sealed record AddGroupMemberRequest(
    Guid UserProfileId);
