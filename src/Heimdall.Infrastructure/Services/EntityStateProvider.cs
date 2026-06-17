using System.Text.Json;
using Heimdall.Application.Common.Interfaces;
using Heimdall.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Heimdall.Infrastructure.Services;

/// <summary>
/// Provides serialized entity state for auditing by querying the DbContext change tracker
/// or performing a direct lookup.
/// </summary>
public class EntityStateProvider : IEntityStateProvider
{
    private readonly HeimdallDbContext _db;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public EntityStateProvider(HeimdallDbContext db)
    {
        _db = db;
    }

    public Task<string?> GetEntityStateAsync(string entityType, Guid entityId, CancellationToken ct = default)
    {
        // Attempt to find the entity in the DbContext by searching tracked entities first
        var entry = _db.ChangeTracker.Entries()
            .FirstOrDefault(e =>
                e.Entity.GetType().Name == entityType &&
                e.Property("Id").CurrentValue is Guid id &&
                id == entityId);

        if (entry is not null)
        {
            return Task.FromResult<string?>(
                JsonSerializer.Serialize(entry.Entity, entry.Entity.GetType(), JsonOptions));
        }

        // If not tracked, entity state is not available
        return Task.FromResult<string?>(null);
    }
}
