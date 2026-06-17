using Heimdall.Application.Users.Commands.DeactivateUser;
using Heimdall.Application.Users.Commands.SyncUser;
using Heimdall.Application.Users.Commands.UpdateUser;
using Heimdall.Application.Users.Queries.GetUser;
using Heimdall.Application.Users.Queries.GetUsers;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Heimdall.Api.Controllers;

[ApiController]
[Route("api/tenants/{tenantId:guid}/users")]
public class UsersController : ControllerBase
{
    private readonly ISender _sender;

    public UsersController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost]
    [ProducesResponseType(typeof(SyncUserResult), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(SyncUserResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> Sync(Guid tenantId, [FromBody] SyncUserRequest request, CancellationToken ct)
    {
        var command = new SyncUserCommand
        {
            TenantId = tenantId,
            ExternalSubjectId = request.ExternalSubjectId,
            IdentityProvider = request.IdentityProvider,
            Email = request.Email,
            DisplayName = request.DisplayName,
            Applications = request.Applications?.Select(a => new SyncUserApplicationEntry
            {
                ApplicationId = a.ApplicationId,
                PermissionTemplateCodes = a.PermissionTemplateCodes ?? []
            }).ToList() ?? []
        };

        var result = await _sender.Send(command, ct);

        if (result.IsNewUser)
            return CreatedAtAction(nameof(GetById), new { tenantId, id = result.UserProfileId }, result);

        return Ok(result);
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<UserDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        Guid tenantId,
        [FromQuery] string? identityProvider,
        [FromQuery] string? status,
        [FromQuery] string? externalSubjectId,
        CancellationToken ct)
    {
        var query = new GetUsersQuery
        {
            IdentityProvider = identityProvider,
            Status = status,
            ExternalSubjectId = externalSubjectId
        };

        var result = await _sender.Send(query, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid tenantId, Guid id, CancellationToken ct)
    {
        var result = await _sender.Send(new GetUserQuery { UserProfileId = id }, ct);
        if (result is null)
            return NotFound();

        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(UpdateUserResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid tenantId, Guid id, [FromBody] UpdateUserRequest request, CancellationToken ct)
    {
        var command = new UpdateUserCommand
        {
            TenantId = tenantId,
            UserProfileId = id,
            Email = request.Email,
            DisplayName = request.DisplayName
        };

        var result = await _sender.Send(command, ct);
        return Ok(result);
    }
}

public sealed record SyncUserRequest(
    string ExternalSubjectId,
    string IdentityProvider,
    string Email,
    string DisplayName,
    List<SyncUserApplicationRequest>? Applications);

public sealed record SyncUserApplicationRequest(
    Guid ApplicationId,
    List<string>? PermissionTemplateCodes);

public sealed record UpdateUserRequest(
    string Email,
    string DisplayName);
