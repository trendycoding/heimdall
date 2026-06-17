using Heimdall.Application.AuditLogs.Queries.GetApiCallLogs;
using Heimdall.Application.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Heimdall.Api.Controllers;

/// <summary>
/// Provides read-only access to API call log records with filtering and pagination.
/// </summary>
[ApiController]
[Route("api/tenants/{tenantId}/api-call-logs")]
public sealed class ApiCallLogsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ApiCallLogsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Retrieve API call logs with optional query filters.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResult<ApiCallLogDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetApiCallLogs(
        [FromRoute] Guid tenantId,
        [FromQuery] Guid? applicationId,
        [FromQuery] Guid? correlationId,
        [FromQuery] string? endpoint,
        [FromQuery] string? httpMethod,
        [FromQuery] string? sourceIp,
        [FromQuery] string? callerSubjectId,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
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

        var query = new GetApiCallLogsQuery
        {
            TenantId = tenantId,
            ApplicationId = applicationId,
            CorrelationId = correlationId,
            Endpoint = endpoint,
            HttpMethod = httpMethod,
            SourceIp = sourceIp,
            CallerSubjectId = callerSubjectId,
            DateFrom = dateFrom,
            DateTo = dateTo,
            Page = page,
            PageSize = pageSize
        };

        var result = await _mediator.Send(query, ct);

        return Ok(result);
    }
}
