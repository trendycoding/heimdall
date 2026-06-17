using Heimdall.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Heimdall.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for EF Core migrations tooling.
/// This allows `dotnet ef migrations add` to create the DbContext
/// without requiring the full application startup pipeline.
/// </summary>
public class HeimdallDbContextFactory : IDesignTimeDbContextFactory<HeimdallDbContext>
{
    public HeimdallDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<HeimdallDbContext>();
        optionsBuilder.UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=Heimdall_Design;Trusted_Connection=True;");

        return new HeimdallDbContext(optionsBuilder.Options, new DesignTimeTenantContext());
    }

    /// <summary>
    /// Minimal ITenantContext implementation for design-time migration generation.
    /// Query filters referencing TenantId will use Guid.Empty at design time,
    /// which is acceptable since no actual queries are executed during migration creation.
    /// </summary>
    private sealed class DesignTimeTenantContext : ITenantContext
    {
        public Guid TenantId => Guid.Empty;
        public Guid? ApplicationId => null;
        public string ActorSubjectId => "design-time";
        public string ActorEmail => "design-time@heimdall.local";
        public Guid? ActorUserProfileId => null;
        public IReadOnlyList<string> AdminScopes => Array.Empty<string>();
        public bool IsSuperAdmin => false;
    }
}
