using Heimdall.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Heimdall.Infrastructure.Persistence;

/// <summary>
/// Specialized repository for AuditLog that supports cross-tenant queries
/// using IgnoreQueryFilters() for super-admin scenarios.
/// </summary>
public class AuditLogRepository : Repository<AuditLog>
{
    public AuditLogRepository(HeimdallDbContext context) : base(context)
    {
    }

    /// <summary>
    /// Gets audit logs across all tenants (bypasses tenant query filter).
    /// Intended for super-admin cross-tenant audit queries.
    /// </summary>
    public async Task<IReadOnlyList<AuditLog>> GetAllUnfilteredAsync(CancellationToken ct = default)
        => await _dbSet.IgnoreQueryFilters().ToListAsync(ct);

    /// <summary>
    /// Gets audit logs for a specific entity across all tenants.
    /// </summary>
    public async Task<IReadOnlyList<AuditLog>> GetByEntityUnfilteredAsync(
        string entityType, string entityId, CancellationToken ct = default)
        => await _dbSet
            .IgnoreQueryFilters()
            .Where(a => a.EntityType == entityType && a.EntityId == entityId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(ct);

    /// <summary>
    /// Gets audit logs by correlation ID across all tenants.
    /// </summary>
    public async Task<IReadOnlyList<AuditLog>> GetByCorrelationIdUnfilteredAsync(
        Guid correlationId, CancellationToken ct = default)
        => await _dbSet
            .IgnoreQueryFilters()
            .Where(a => a.CorrelationId == correlationId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(ct);

    /// <summary>
    /// Gets audit logs for a specific tenant (bypasses global filter but applies explicit tenant filter).
    /// Useful when querying on behalf of a specific tenant from a super-admin context.
    /// </summary>
    public async Task<IReadOnlyList<AuditLog>> GetByTenantIdAsync(
        Guid tenantId, CancellationToken ct = default)
        => await _dbSet
            .IgnoreQueryFilters()
            .Where(a => a.TenantId == tenantId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(ct);
}
