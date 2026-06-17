using System.Text.Json;
using Heimdall.Application.Common.Interfaces;
using Heimdall.Domain.Interfaces;
using Heimdall.Domain.Models;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace Heimdall.Application.Common.Behaviors;

/// <summary>
/// Pipeline behavior that captures before/after entity state for auditable commands
/// and records an audit entry via <see cref="IAuditService"/>.
/// The audit record is added to the same DbContext and committed in a single SaveChangesAsync call,
/// ensuring transactional consistency — if audit creation fails, the entity change rolls back.
/// </summary>
public sealed class AuditBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IAuditService _auditService;
    private readonly ITenantContext _tenantContext;
    private readonly IEntityStateProvider _entityStateProvider;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IHeimdallDbContext _dbContext;

    public AuditBehavior(
        IAuditService auditService,
        ITenantContext tenantContext,
        IEntityStateProvider entityStateProvider,
        IHttpContextAccessor httpContextAccessor,
        IHeimdallDbContext dbContext)
    {
        _auditService = auditService;
        _tenantContext = tenantContext;
        _entityStateProvider = entityStateProvider;
        _httpContextAccessor = httpContextAccessor;
        _dbContext = dbContext;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is not IAuditableCommand auditableCommand)
        {
            return await next();
        }

        // Capture before state (null for create operations)
        var beforeJson = await _entityStateProvider.GetEntityStateAsync(
            auditableCommand.EntityType,
            auditableCommand.EntityId,
            cancellationToken);

        var response = await next();

        // Capture after state (null for delete operations)
        var afterJson = await _entityStateProvider.GetEntityStateAsync(
            auditableCommand.EntityType,
            auditableCommand.EntityId,
            cancellationToken);

        // Extract request context from HttpContext
        var httpContext = _httpContextAccessor.HttpContext;
        var correlationId = ResolveCorrelationId(httpContext);
        var apiCallLogId = ResolveApiCallLogId(httpContext);
        var sourceIp = httpContext?.Connection.RemoteIpAddress?.ToString();
        var userAgent = httpContext?.Request.Headers.UserAgent.ToString();

        var auditEntry = new AuditEntry
        {
            TenantId = _tenantContext.TenantId,
            ApplicationId = _tenantContext.ApplicationId,
            EntityType = auditableCommand.EntityType,
            EntityId = auditableCommand.EntityId,
            Action = auditableCommand.AuditAction,
            BeforeJson = beforeJson,
            AfterJson = afterJson,
            ChangedFieldsJson = ComputeChangedFields(beforeJson, afterJson),
            ActorSubjectId = _tenantContext.ActorSubjectId,
            ActorEmail = _tenantContext.ActorEmail,
            ActorUserProfileId = _tenantContext.ActorUserProfileId,
            CorrelationId = correlationId,
            ApiCallLogId = apiCallLogId,
            SourceIp = sourceIp,
            UserAgent = userAgent
        };

        // RecordAsync adds the AuditLog to the DbContext without saving.
        // We then call SaveChangesAsync to commit the audit record.
        // Since we share the same DbContext, if the audit write fails,
        // the SaveChangesAsync throws and the operation fails (Requirement 17.5).
        await _auditService.RecordAsync(auditEntry, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return response;
    }

    private static Guid? ResolveCorrelationId(HttpContext? httpContext)
    {
        if (httpContext?.Items.TryGetValue("CorrelationId", out var id) == true &&
            Guid.TryParse(id?.ToString(), out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static Guid? ResolveApiCallLogId(HttpContext? httpContext)
    {
        if (httpContext?.Items.TryGetValue("ApiCallLogId", out var id) == true)
        {
            if (id is Guid guidValue)
            {
                return guidValue;
            }

            if (Guid.TryParse(id?.ToString(), out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static string? ComputeChangedFields(string? beforeJson, string? afterJson)
    {
        if (beforeJson is null || afterJson is null)
        {
            return null;
        }

        try
        {
            using var beforeDoc = JsonDocument.Parse(beforeJson);
            using var afterDoc = JsonDocument.Parse(afterJson);

            var changedFields = new List<string>();

            foreach (var property in afterDoc.RootElement.EnumerateObject())
            {
                if (!beforeDoc.RootElement.TryGetProperty(property.Name, out var beforeValue) ||
                    beforeValue.GetRawText() != property.Value.GetRawText())
                {
                    changedFields.Add(property.Name);
                }
            }

            return changedFields.Count > 0
                ? JsonSerializer.Serialize(changedFields)
                : null;
        }
        catch
        {
            return null;
        }
    }
}
