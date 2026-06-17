namespace Heimdall.Application.Common.Interfaces;

/// <summary>
/// Provides serialized entity state for auditing purposes.
/// Infrastructure layer implementations query the database to capture entity snapshots.
/// </summary>
public interface IEntityStateProvider
{
    /// <summary>
    /// Returns the JSON-serialized state of an entity, or null if the entity does not exist.
    /// </summary>
    Task<string?> GetEntityStateAsync(string entityType, Guid entityId, CancellationToken ct = default);
}
