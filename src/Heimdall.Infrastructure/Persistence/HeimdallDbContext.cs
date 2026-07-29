using Heimdall.Application.Common.Interfaces;
using Heimdall.Domain.Entities;
using Heimdall.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using ApplicationEntity = Heimdall.Domain.Entities.Application;

namespace Heimdall.Infrastructure.Persistence;

public class HeimdallDbContext : DbContext, IHeimdallDbContext
{
    private readonly ITenantContext _tenantContext;

    /// <summary>
    /// When true, SaveChangesAsync will not auto-populate CreatedAt/ModifiedAt/CreatedBy/ModifiedBy.
    /// Used by the development seed data service to preserve explicitly set timestamps.
    /// </summary>
    public bool SuppressAutoTimestamps { get; set; }

    public HeimdallDbContext(
        DbContextOptions<HeimdallDbContext> options,
        ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    // Non-tenant-scoped entities
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantMembership> TenantMemberships => Set<TenantMembership>();
    public DbSet<ApiKeyRegistration> ApiKeyRegistrations => Set<ApiKeyRegistration>();
    public DbSet<PermissionTemplatePermission> PermissionTemplatePermissions => Set<PermissionTemplatePermission>();
    public DbSet<PermissionTemplateGroup> PermissionTemplateGroups => Set<PermissionTemplateGroup>();
    public DbSet<PermissionTemplateAccessDetail> PermissionTemplateAccessDetails => Set<PermissionTemplateAccessDetail>();

    // Tenant-scoped entities
    public DbSet<ApplicationEntity> Applications => Set<ApplicationEntity>();
    public DbSet<IdentityProviderConfiguration> IdentityProviderConfigurations => Set<IdentityProviderConfiguration>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<UserPermissionTemplateApplication> UserPermissionTemplateApplications => Set<UserPermissionTemplateApplication>();
    public DbSet<ApiCallLog> ApiCallLogs => Set<ApiCallLog>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    // Application-scoped entities (inherit TenantScopedEntity)
    public DbSet<FunctionalArea> FunctionalAreas => Set<FunctionalArea>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<PermissionType> PermissionTypes => Set<PermissionType>();
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<GroupMembership> GroupMemberships => Set<GroupMembership>();
    public DbSet<UserPermissionAssignment> UserPermissionAssignments => Set<UserPermissionAssignment>();
    public DbSet<GroupPermissionAssignment> GroupPermissionAssignments => Set<GroupPermissionAssignment>();
    public DbSet<UserAccessDetail> UserAccessDetails => Set<UserAccessDetail>();
    public DbSet<GroupAccessDetail> GroupAccessDetails => Set<GroupAccessDetail>();
    public DbSet<FunctionalAreaAccessRequirement> FunctionalAreaAccessRequirements => Set<FunctionalAreaAccessRequirement>();
    public DbSet<PermissionTemplate> PermissionTemplates => Set<PermissionTemplate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all IEntityTypeConfiguration implementations from this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HeimdallDbContext).Assembly);

        // Apply global query filters for all tenant-scoped entities
        modelBuilder.Entity<ApplicationEntity>().HasQueryFilter(e => e.TenantId == _tenantContext.TenantId);
        modelBuilder.Entity<IdentityProviderConfiguration>().HasQueryFilter(e => e.TenantId == _tenantContext.TenantId);
        modelBuilder.Entity<UserProfile>().HasQueryFilter(e => e.TenantId == _tenantContext.TenantId);
        modelBuilder.Entity<UserPermissionTemplateApplication>().HasQueryFilter(e => e.TenantId == _tenantContext.TenantId);
        modelBuilder.Entity<ApiCallLog>().HasQueryFilter(e => e.TenantId == _tenantContext.TenantId);
        modelBuilder.Entity<AuditLog>().HasQueryFilter(e => e.TenantId == _tenantContext.TenantId);

        // Application-scoped entities also get tenant filter (they inherit TenantScopedEntity)
        modelBuilder.Entity<FunctionalArea>().HasQueryFilter(e => e.TenantId == _tenantContext.TenantId);
        modelBuilder.Entity<Permission>().HasQueryFilter(e => e.TenantId == _tenantContext.TenantId);
        modelBuilder.Entity<PermissionType>().HasQueryFilter(e => e.TenantId == _tenantContext.TenantId);
        modelBuilder.Entity<Group>().HasQueryFilter(e => e.TenantId == _tenantContext.TenantId);
        modelBuilder.Entity<GroupMembership>().HasQueryFilter(e => e.TenantId == _tenantContext.TenantId);
        modelBuilder.Entity<UserPermissionAssignment>().HasQueryFilter(e => e.TenantId == _tenantContext.TenantId);
        modelBuilder.Entity<GroupPermissionAssignment>().HasQueryFilter(e => e.TenantId == _tenantContext.TenantId);
        modelBuilder.Entity<UserAccessDetail>().HasQueryFilter(e => e.TenantId == _tenantContext.TenantId);
        modelBuilder.Entity<GroupAccessDetail>().HasQueryFilter(e => e.TenantId == _tenantContext.TenantId);
        modelBuilder.Entity<FunctionalAreaAccessRequirement>().HasQueryFilter(e => e.TenantId == _tenantContext.TenantId);
        modelBuilder.Entity<PermissionTemplate>().HasQueryFilter(e => e.TenantId == _tenantContext.TenantId);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        EnforceLogImmutability();

        if (!SuppressAutoTimestamps)
        {
            var entries = ChangeTracker.Entries<BaseEntity>();

            foreach (var entry in entries)
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        entry.Entity.CreatedAt = DateTime.UtcNow;
                        entry.Entity.CreatedBy = _tenantContext.ActorEmail;
                        break;

                    case EntityState.Modified:
                        entry.Entity.ModifiedAt = DateTime.UtcNow;
                        entry.Entity.ModifiedBy = _tenantContext.ActorEmail;
                        break;
                }
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Enforces immutability for AuditLog and ApiCallLog entities.
    /// AuditLog: Rejects all modifications and deletions (Requirements 17.4).
    /// ApiCallLog: Rejects all deletions (Requirements 16.3). Modifications are allowed
    /// only for the completion fields (StatusCode, DurationMs, ResponseTimestamp) set by
    /// ApiCallLogService.CompleteLogAsync.
    /// </summary>
    private void EnforceLogImmutability()
    {
        var auditLogEntries = ChangeTracker.Entries<AuditLog>();
        foreach (var entry in auditLogEntries)
        {
            if (entry.State == EntityState.Modified)
            {
                throw new InvalidOperationException(
                    "AuditLog records are immutable and cannot be modified after creation.");
            }

            if (entry.State == EntityState.Deleted)
            {
                throw new InvalidOperationException(
                    "AuditLog records are immutable and cannot be deleted.");
            }
        }

        var apiCallLogEntries = ChangeTracker.Entries<ApiCallLog>();
        foreach (var entry in apiCallLogEntries)
        {
            if (entry.State == EntityState.Deleted)
            {
                throw new InvalidOperationException(
                    "ApiCallLog records are immutable and cannot be deleted.");
            }

            if (entry.State == EntityState.Modified)
            {
                // Allow only completion fields to be modified (set by ApiCallLogService.CompleteLogAsync)
                var modifiedProperties = entry.Properties
                    .Where(p => p.IsModified)
                    .Select(p => p.Metadata.Name)
                    .ToHashSet();

                var allowedCompletionFields = new HashSet<string>
                {
                    nameof(ApiCallLog.StatusCode),
                    nameof(ApiCallLog.DurationMs),
                    nameof(ApiCallLog.ResponseTimestamp),
                    nameof(BaseEntity.ModifiedAt),
                    nameof(BaseEntity.ModifiedBy)
                };

                var disallowedModifications = modifiedProperties.Except(allowedCompletionFields).ToList();
                if (disallowedModifications.Any())
                {
                    throw new InvalidOperationException(
                        $"ApiCallLog records are immutable. Only completion fields (StatusCode, DurationMs, ResponseTimestamp) may be updated. " +
                        $"Attempted to modify: {string.Join(", ", disallowedModifications)}");
                }
            }
        }
    }
}
