using Heimdall.Domain.Models;

namespace Heimdall.Application.Common.Interfaces;

/// <summary>
/// Marker interface for commands that should produce an audit trail.
/// Implementations provide entity context for before/after state capture.
/// </summary>
public interface IAuditableCommand
{
    /// <summary>
    /// The type name of the entity being modified (e.g., "Tenant", "Application").
    /// </summary>
    string EntityType { get; }

    /// <summary>
    /// The unique identifier of the entity being modified.
    /// </summary>
    Guid EntityId { get; }

    /// <summary>
    /// The action being performed (e.g., "Create", "Update", "Deactivate").
    /// </summary>
    string AuditAction { get; }
}
