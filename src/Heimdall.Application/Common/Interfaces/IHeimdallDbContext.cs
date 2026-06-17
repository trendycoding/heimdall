using Heimdall.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using ApplicationEntity = Heimdall.Domain.Entities.Application;

namespace Heimdall.Application.Common.Interfaces;

/// <summary>
/// Abstraction over the DbContext for use in the Application layer.
/// Provides direct query access for scenarios not covered by generic IRepository
/// (e.g., cross-tenant slug uniqueness checks, cross-app reference validation).
/// </summary>
public interface IHeimdallDbContext
{
    DbSet<Tenant> Tenants { get; }
    DbSet<ApplicationEntity> Applications { get; }
    DbSet<IdentityProviderConfiguration> IdentityProviderConfigurations { get; }
    DbSet<FunctionalArea> FunctionalAreas { get; }
    DbSet<PermissionType> PermissionTypes { get; }
    DbSet<Permission> Permissions { get; }
    DbSet<UserProfile> UserProfiles { get; }
    DbSet<Group> Groups { get; }
    DbSet<GroupMembership> GroupMemberships { get; }
    DbSet<UserPermissionAssignment> UserPermissionAssignments { get; }
    DbSet<GroupPermissionAssignment> GroupPermissionAssignments { get; }
    DbSet<UserAccessDetail> UserAccessDetails { get; }
    DbSet<GroupAccessDetail> GroupAccessDetails { get; }
    DbSet<FunctionalAreaAccessRequirement> FunctionalAreaAccessRequirements { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<ApiCallLog> ApiCallLogs { get; }
    DbSet<PermissionTemplate> PermissionTemplates { get; }
    DbSet<PermissionTemplatePermission> PermissionTemplatePermissions { get; }
    DbSet<PermissionTemplateGroup> PermissionTemplateGroups { get; }
    DbSet<PermissionTemplateAccessDetail> PermissionTemplateAccessDetails { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
