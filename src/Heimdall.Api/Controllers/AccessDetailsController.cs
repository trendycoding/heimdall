using Heimdall.Application.AccessDetails.Commands.CreateFunctionalAreaAccessRequirement;
using Heimdall.Application.AccessDetails.Commands.CreateGroupAccessDetail;
using Heimdall.Application.AccessDetails.Commands.CreateUserAccessDetail;
using Heimdall.Application.AccessDetails.Commands.DeactivateFunctionalAreaAccessRequirement;
using Heimdall.Application.AccessDetails.Commands.DeactivateGroupAccessDetail;
using Heimdall.Application.AccessDetails.Commands.DeactivateUserAccessDetail;
using Heimdall.Application.AccessDetails.Commands.UpdateFunctionalAreaAccessRequirement;
using Heimdall.Application.AccessDetails.Commands.UpdateGroupAccessDetail;
using Heimdall.Application.AccessDetails.Commands.UpdateUserAccessDetail;
using Heimdall.Application.AccessDetails.Queries.GetFunctionalAreaAccessRequirements;
using Heimdall.Application.AccessDetails.Queries.GetGroupAccessDetails;
using Heimdall.Application.AccessDetails.Queries.GetUserAccessDetails;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Heimdall.Api.Controllers;

[ApiController]
[Route("api/tenants/{tenantId:guid}/applications/{appId:guid}/access-details")]
public class AccessDetailsController : ControllerBase
{
    private readonly ISender _sender;

    public AccessDetailsController(ISender sender)
    {
        _sender = sender;
    }

    // User access details

    [HttpPost("users")]
    [ProducesResponseType(typeof(CreateUserAccessDetailResult), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateUserAccessDetail(Guid tenantId, Guid appId, [FromBody] CreateUserAccessDetailRequest request, CancellationToken ct)
    {
        var command = new CreateUserAccessDetailCommand
        {
            TenantId = tenantId,
            ApplicationId = appId,
            UserProfileId = request.UserProfileId,
            AccessDetailType = request.AccessDetailType,
            AccessDetailCode = request.AccessDetailCode,
            AccessDetailValue = request.AccessDetailValue,
            Description = request.Description,
            ValidFrom = request.ValidFrom,
            ValidTo = request.ValidTo
        };

        var result = await _sender.Send(command, ct);
        return CreatedAtAction(nameof(GetUserAccessDetails), new { tenantId, appId, userProfileId = request.UserProfileId }, result);
    }

    [HttpGet("users/{userProfileId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<UserAccessDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserAccessDetails(Guid tenantId, Guid appId, Guid userProfileId, CancellationToken ct)
    {
        var result = await _sender.Send(new GetUserAccessDetailsQuery(userProfileId, appId), ct);
        return Ok(result);
    }

    [HttpPut("users/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UpdateUserAccessDetail(Guid tenantId, Guid appId, Guid id, [FromBody] UpdateUserAccessDetailRequest request, CancellationToken ct)
    {
        var command = new UpdateUserAccessDetailCommand
        {
            Id = id,
            AccessDetailValue = request.AccessDetailValue,
            Description = request.Description,
            ValidFrom = request.ValidFrom,
            ValidTo = request.ValidTo
        };

        await _sender.Send(command, ct);
        return NoContent();
    }

    [HttpDelete("users/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteUserAccessDetail(Guid tenantId, Guid appId, Guid id, CancellationToken ct)
    {
        await _sender.Send(new DeactivateUserAccessDetailCommand { Id = id }, ct);
        return NoContent();
    }

    // Group access details

    [HttpPost("groups")]
    [ProducesResponseType(typeof(CreateGroupAccessDetailResult), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateGroupAccessDetail(Guid tenantId, Guid appId, [FromBody] CreateGroupAccessDetailRequest request, CancellationToken ct)
    {
        var command = new CreateGroupAccessDetailCommand
        {
            TenantId = tenantId,
            ApplicationId = appId,
            GroupId = request.GroupId,
            AccessDetailType = request.AccessDetailType,
            AccessDetailCode = request.AccessDetailCode,
            AccessDetailValue = request.AccessDetailValue,
            Description = request.Description,
            ValidFrom = request.ValidFrom,
            ValidTo = request.ValidTo
        };

        var result = await _sender.Send(command, ct);
        return CreatedAtAction(nameof(GetGroupAccessDetails), new { tenantId, appId, groupId = request.GroupId }, result);
    }

    [HttpGet("groups/{groupId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<GroupAccessDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetGroupAccessDetails(Guid tenantId, Guid appId, Guid groupId, CancellationToken ct)
    {
        var result = await _sender.Send(new GetGroupAccessDetailsQuery(groupId, appId), ct);
        return Ok(result);
    }

    [HttpPut("groups/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UpdateGroupAccessDetail(Guid tenantId, Guid appId, Guid id, [FromBody] UpdateGroupAccessDetailRequest request, CancellationToken ct)
    {
        var command = new UpdateGroupAccessDetailCommand
        {
            Id = id,
            AccessDetailValue = request.AccessDetailValue,
            Description = request.Description,
            ValidFrom = request.ValidFrom,
            ValidTo = request.ValidTo
        };

        await _sender.Send(command, ct);
        return NoContent();
    }

    [HttpDelete("groups/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteGroupAccessDetail(Guid tenantId, Guid appId, Guid id, CancellationToken ct)
    {
        await _sender.Send(new DeactivateGroupAccessDetailCommand { Id = id }, ct);
        return NoContent();
    }

    // Functional Area access requirements

    [HttpPost("functional-areas/{functionalAreaId:guid}/requirements")]
    [ProducesResponseType(typeof(CreateFunctionalAreaAccessRequirementResult), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateFunctionalAreaRequirement(
        Guid tenantId, Guid appId, Guid functionalAreaId, [FromBody] CreateFunctionalAreaAccessRequirementRequest request, CancellationToken ct)
    {
        var command = new CreateFunctionalAreaAccessRequirementCommand
        {
            TenantId = tenantId,
            ApplicationId = appId,
            FunctionalAreaId = functionalAreaId,
            AccessDetailType = request.AccessDetailType,
            IsRequired = request.IsRequired,
            Description = request.Description
        };

        var result = await _sender.Send(command, ct);
        return CreatedAtAction(nameof(GetFunctionalAreaRequirements), new { tenantId, appId, functionalAreaId }, result);
    }

    [HttpGet("functional-areas/{functionalAreaId:guid}/requirements")]
    [ProducesResponseType(typeof(IReadOnlyList<FunctionalAreaAccessRequirementDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFunctionalAreaRequirements(Guid tenantId, Guid appId, Guid functionalAreaId, CancellationToken ct)
    {
        var result = await _sender.Send(new GetFunctionalAreaAccessRequirementsQuery(functionalAreaId), ct);
        return Ok(result);
    }

    [HttpPut("functional-areas/requirements/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UpdateFunctionalAreaRequirement(Guid tenantId, Guid appId, Guid id, [FromBody] UpdateFunctionalAreaAccessRequirementRequest request, CancellationToken ct)
    {
        var command = new UpdateFunctionalAreaAccessRequirementCommand
        {
            Id = id,
            IsRequired = request.IsRequired,
            Description = request.Description
        };

        await _sender.Send(command, ct);
        return NoContent();
    }

    [HttpDelete("functional-areas/requirements/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteFunctionalAreaRequirement(Guid tenantId, Guid appId, Guid id, CancellationToken ct)
    {
        await _sender.Send(new DeactivateFunctionalAreaAccessRequirementCommand { Id = id }, ct);
        return NoContent();
    }
}

public sealed record CreateUserAccessDetailRequest(
    Guid UserProfileId,
    string AccessDetailType,
    string AccessDetailCode,
    string AccessDetailValue,
    string? Description,
    DateTime? ValidFrom,
    DateTime? ValidTo);

public sealed record UpdateUserAccessDetailRequest(
    string AccessDetailValue,
    string? Description,
    DateTime? ValidFrom,
    DateTime? ValidTo);

public sealed record CreateGroupAccessDetailRequest(
    Guid GroupId,
    string AccessDetailType,
    string AccessDetailCode,
    string AccessDetailValue,
    string? Description,
    DateTime? ValidFrom,
    DateTime? ValidTo);

public sealed record UpdateGroupAccessDetailRequest(
    string AccessDetailValue,
    string? Description,
    DateTime? ValidFrom,
    DateTime? ValidTo);

public sealed record CreateFunctionalAreaAccessRequirementRequest(
    string AccessDetailType,
    bool IsRequired,
    string? Description);

public sealed record UpdateFunctionalAreaAccessRequirementRequest(
    bool IsRequired,
    string? Description);
