using Heimdall.Application.AuditLogs.Queries.GetAuditLogs;
using Heimdall.Application.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Heimdall.Api.Controllers;

/// <summary>
/// Provides read-only access to audit log records with filtering and pagination.
/// </summary>
[ApiController]
[Route("api/tenants/{tenantId}/audit-logs")]
public sealed class AuditLogsController : ControllerBase
{
    private readonly IMediator _mediator;

    public AuditLogsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Retrieve audit logs with optional query filters. Results are sorted by CreatedAt DESC
    /// and paginated with a maximum page size of 100.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResult<AuditLogDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetAuditLogs(
        [FromRoute] Guid tenantId,
        [FromQuery] Guid? applicationId,
        [FromQuery] string? entityType,
        [FromQuery] string? entityId,
        [FromQuery] string? action,
        [FromQuery] Guid? actorUserProfileId,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] Guid? correlationId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        if (page < 1)
        {
            return BadRequest("Page must be at least 1.");
        }

        if (pageSize < 1 || pageSize > 100)
        {
            return BadRequest("PageSize must be between 1 and 100.");
        }

        var query = new GetAuditLogsQuery
        {
            TenantId = tenantId,
            ApplicationId = applicationId,
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            ActorUserProfileId = actorUserProfileId,
            DateFrom = dateFrom,
            DateTo = dateTo,
            CorrelationId = correlationId,
            Page = page,
            PageSize = pageSize
        };

        var result = await _mediator.Send(query, ct);

        return Ok(result);
    }
}
