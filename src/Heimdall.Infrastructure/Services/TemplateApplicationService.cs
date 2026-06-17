using Heimdall.Domain.Entities;
using Heimdall.Domain.Enums;
using Heimdall.Domain.Interfaces;
using Heimdall.Domain.Models;
using Heimdall.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Heimdall.Infrastructure.Services;

public class TemplateApplicationService : ITemplateApplicationService
{
    private readonly HeimdallDbContext _db;
    private readonly ICacheService _cache;
    private readonly ITenantContext _tenantContext;

    public TemplateApplicationService(
        HeimdallDbContext db,
        ICacheService cache,
        ITenantContext tenantContext)
    {
        _db = db;
        _cache = cache;
        _tenantContext = tenantContext;
    }

    public async Task<TemplateApplicationResult> ApplyTemplateAsync(
        Guid tenantId, Guid applicationId, Guid userProfileId,
        Guid permissionTemplateId, TemplateApplicationOptions options,
        CancellationToken ct = default)
    {
        // Step 1: Look up template and validate IsActive
        var template = await _db.PermissionTemplates
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t =>
                t.Id == permissionTemplateId &&
                t.TenantId == tenantId &&
                t.ApplicationId == applicationId, ct);

        if (template is null)
        {
            return new TemplateApplicationResult
            {
                Success = false,
                Warnings = ["Permission template not found"]
            };
        }

        if (!template.IsActive)
        {
            return new TemplateApplicationResult
            {
                Success = false,
                Warnings = ["Permission template is inactive"]
            };
        }

        // Step 2: Collect all template entries
        var templatePermissions = await _db.PermissionTemplatePermissions
            .IgnoreQueryFilters()
            .Where(tp => tp.PermissionTemplateId == permissionTemplateId)
            .ToListAsync(ct);

        var templateGroups = await _db.PermissionTemplateGroups
            .IgnoreQueryFilters()
            .Where(tg => tg.PermissionTemplateId == permissionTemplateId)
            .ToListAsync(ct);

        var templateAccessDetails = await _db.PermissionTemplateAccessDetails
            .IgnoreQueryFilters()
            .Where(tad => tad.PermissionTemplateId == permissionTemplateId)
            .ToListAsync(ct);

        var now = DateTime.UtcNow;
        var warnings = new List<string>();
        int permissionsApplied = 0;
        int groupMembershipsApplied = 0;
        int accessDetailsApplied = 0;

        // Use a strategy to wrap all operations within one SaveChanges call (single transaction)
        using var transaction = await _db.Database.BeginTransactionAsync(ct);

        try
        {
            // Step 3: Process permissions
            if (options.ReplaceExistingPermissions)
            {
                var existingAssignments = await _db.UserPermissionAssignments
                    .IgnoreQueryFilters()
                    .Where(a =>
                        a.TenantId == tenantId &&
                        a.ApplicationId == applicationId &&
                        a.UserProfileId == userProfileId)
                    .ToListAsync(ct);

                _db.UserPermissionAssignments.RemoveRange(existingAssignments);
            }

            foreach (var tp in templatePermissions)
            {
                if (!options.ReplaceExistingPermissions)
                {
                    // Skip duplicates
                    var exists = await _db.UserPermissionAssignments
                        .IgnoreQueryFilters()
                        .AnyAsync(a =>
                            a.TenantId == tenantId &&
                            a.ApplicationId == applicationId &&
                            a.UserProfileId == userProfileId &&
                            a.PermissionId == tp.PermissionId &&
                            a.Effect == tp.Effect, ct);

                    if (exists)
                        continue;
                }

                var assignment = new UserPermissionAssignment
                {
                    TenantId = tenantId,
                    ApplicationId = applicationId,
                    UserProfileId = userProfileId,
                    PermissionId = tp.PermissionId,
                    Effect = tp.Effect,
                    ValidFrom = tp.ValidFromOffsetDays.HasValue ? now.AddDays(tp.ValidFromOffsetDays.Value) : null,
                    ValidTo = tp.ValidToOffsetDays.HasValue ? now.AddDays(tp.ValidToOffsetDays.Value) : null
                };

                _db.UserPermissionAssignments.Add(assignment);
                permissionsApplied++;
            }

            // Step 4: Process groups
            if (options.ReplaceExistingGroups)
            {
                var existingMemberships = await _db.GroupMemberships
                    .IgnoreQueryFilters()
                    .Where(gm =>
                        gm.TenantId == tenantId &&
                        gm.ApplicationId == applicationId &&
                        gm.UserProfileId == userProfileId)
                    .ToListAsync(ct);

                _db.GroupMemberships.RemoveRange(existingMemberships);
            }

            foreach (var tg in templateGroups)
            {
                if (!options.ReplaceExistingGroups)
                {
                    // Skip duplicates
                    var exists = await _db.GroupMemberships
                        .IgnoreQueryFilters()
                        .AnyAsync(gm =>
                            gm.TenantId == tenantId &&
                            gm.ApplicationId == applicationId &&
                            gm.UserProfileId == userProfileId &&
                            gm.GroupId == tg.GroupId, ct);

                    if (exists)
                        continue;
                }

                var membership = new GroupMembership
                {
                    TenantId = tenantId,
                    ApplicationId = applicationId,
                    UserProfileId = userProfileId,
                    GroupId = tg.GroupId
                };

                _db.GroupMemberships.Add(membership);
                groupMembershipsApplied++;
            }

            // Step 5: Process access details
            if (options.ReplaceExistingAccessDetails)
            {
                var existingAccessDetails = await _db.UserAccessDetails
                    .IgnoreQueryFilters()
                    .Where(ad =>
                        ad.TenantId == tenantId &&
                        ad.ApplicationId == applicationId &&
                        ad.UserProfileId == userProfileId)
                    .ToListAsync(ct);

                _db.UserAccessDetails.RemoveRange(existingAccessDetails);
            }

            foreach (var tad in templateAccessDetails)
            {
                if (!options.ReplaceExistingAccessDetails)
                {
                    // Skip duplicates (by type + code)
                    var exists = await _db.UserAccessDetails
                        .IgnoreQueryFilters()
                        .AnyAsync(ad =>
                            ad.TenantId == tenantId &&
                            ad.ApplicationId == applicationId &&
                            ad.UserProfileId == userProfileId &&
                            ad.AccessDetailType == tad.AccessDetailType &&
                            ad.AccessDetailCode == tad.AccessDetailCode, ct);

                    if (exists)
                        continue;
                }

                var accessDetail = new UserAccessDetail
                {
                    TenantId = tenantId,
                    ApplicationId = applicationId,
                    UserProfileId = userProfileId,
                    AccessDetailType = tad.AccessDetailType,
                    AccessDetailCode = tad.AccessDetailCode,
                    AccessDetailValue = tad.AccessDetailValue,
                    Description = tad.Description,
                    IsActive = true,
                    ValidFrom = tad.ValidFromOffsetDays.HasValue ? now.AddDays(tad.ValidFromOffsetDays.Value) : null,
                    ValidTo = tad.ValidToOffsetDays.HasValue ? now.AddDays(tad.ValidToOffsetDays.Value) : null
                };

                _db.UserAccessDetails.Add(accessDetail);
                accessDetailsApplied++;
            }

            // Step 6: Create audit trail record
            var applicationRecord = new UserPermissionTemplateApplication
            {
                TenantId = tenantId,
                ApplicationId = applicationId,
                UserProfileId = userProfileId,
                PermissionTemplateId = permissionTemplateId,
                AppliedAt = now,
                AppliedBy = _tenantContext.ActorEmail,
                SourceIp = null,
                UserAgent = null,
                CorrelationId = Guid.NewGuid(),
                ApiCallLogId = null
            };

            _db.UserPermissionTemplateApplications.Add(applicationRecord);

            // Step 7: Persist all changes in a single transaction
            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            // Step 8: Invalidate cache for the user
            await InvalidateUserCacheAsync(tenantId, applicationId, userProfileId, ct);

            return new TemplateApplicationResult
            {
                Success = true,
                PermissionsApplied = permissionsApplied,
                GroupMembershipsApplied = groupMembershipsApplied,
                AccessDetailsApplied = accessDetailsApplied,
                Warnings = warnings
            };
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<TemplatePreviewResult> PreviewTemplateAsync(
        Guid tenantId, Guid applicationId, Guid userProfileId,
        Guid permissionTemplateId, TemplateApplicationOptions options,
        CancellationToken ct = default)
    {
        // Step 1: Look up template and validate IsActive
        var template = await _db.PermissionTemplates
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t =>
                t.Id == permissionTemplateId &&
                t.TenantId == tenantId &&
                t.ApplicationId == applicationId, ct);

        if (template is null || !template.IsActive)
        {
            return new TemplatePreviewResult();
        }

        // Step 2: Collect template entries
        var templatePermissions = await _db.PermissionTemplatePermissions
            .IgnoreQueryFilters()
            .Where(tp => tp.PermissionTemplateId == permissionTemplateId)
            .ToListAsync(ct);

        var templateGroups = await _db.PermissionTemplateGroups
            .IgnoreQueryFilters()
            .Where(tg => tg.PermissionTemplateId == permissionTemplateId)
            .ToListAsync(ct);

        var templateAccessDetails = await _db.PermissionTemplateAccessDetails
            .IgnoreQueryFilters()
            .Where(tad => tad.PermissionTemplateId == permissionTemplateId)
            .ToListAsync(ct);

        // Step 3: Compute projected permission changes
        var permissionChanges = new List<ProjectedPermissionChange>();

        var existingAssignments = await _db.UserPermissionAssignments
            .IgnoreQueryFilters()
            .Where(a =>
                a.TenantId == tenantId &&
                a.ApplicationId == applicationId &&
                a.UserProfileId == userProfileId)
            .ToListAsync(ct);

        // Load permission codes for display
        var permissionIds = templatePermissions.Select(tp => tp.PermissionId)
            .Union(existingAssignments.Select(a => a.PermissionId))
            .Distinct()
            .ToList();

        var permissionLookup = await _db.Permissions
            .IgnoreQueryFilters()
            .Where(p => permissionIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.PermissionCode, ct);

        if (options.ReplaceExistingPermissions)
        {
            // Existing assignments that will be removed
            foreach (var existing in existingAssignments)
            {
                var willBeReplaced = templatePermissions.Any(tp =>
                    tp.PermissionId == existing.PermissionId && tp.Effect == existing.Effect);

                permissionLookup.TryGetValue(existing.PermissionId, out var code);

                if (!willBeReplaced)
                {
                    permissionChanges.Add(new ProjectedPermissionChange
                    {
                        PermissionId = existing.PermissionId,
                        PermissionCode = code ?? string.Empty,
                        Effect = existing.Effect.ToString(),
                        ChangeType = "Remove"
                    });
                }
            }

            // All template permissions will be added
            foreach (var tp in templatePermissions)
            {
                var alreadyExists = existingAssignments.Any(a =>
                    a.PermissionId == tp.PermissionId && a.Effect == tp.Effect);

                permissionLookup.TryGetValue(tp.PermissionId, out var code);

                permissionChanges.Add(new ProjectedPermissionChange
                {
                    PermissionId = tp.PermissionId,
                    PermissionCode = code ?? string.Empty,
                    Effect = tp.Effect.ToString(),
                    ChangeType = alreadyExists ? "Unchanged" : "Add"
                });
            }
        }
        else
        {
            // Only add new ones (skip duplicates)
            foreach (var tp in templatePermissions)
            {
                var exists = existingAssignments.Any(a =>
                    a.PermissionId == tp.PermissionId && a.Effect == tp.Effect);

                permissionLookup.TryGetValue(tp.PermissionId, out var code);

                permissionChanges.Add(new ProjectedPermissionChange
                {
                    PermissionId = tp.PermissionId,
                    PermissionCode = code ?? string.Empty,
                    Effect = tp.Effect.ToString(),
                    ChangeType = exists ? "Unchanged" : "Add"
                });
            }
        }

        // Step 4: Compute projected group changes
        var groupChanges = new List<ProjectedGroupChange>();

        var existingMemberships = await _db.GroupMemberships
            .IgnoreQueryFilters()
            .Where(gm =>
                gm.TenantId == tenantId &&
                gm.ApplicationId == applicationId &&
                gm.UserProfileId == userProfileId)
            .ToListAsync(ct);

        var groupIds = templateGroups.Select(tg => tg.GroupId)
            .Union(existingMemberships.Select(gm => gm.GroupId))
            .Distinct()
            .ToList();

        var groupLookup = await _db.Groups
            .IgnoreQueryFilters()
            .Where(g => groupIds.Contains(g.Id))
            .ToDictionaryAsync(g => g.Id, g => g.Name, ct);

        if (options.ReplaceExistingGroups)
        {
            foreach (var existing in existingMemberships)
            {
                var willBeReplaced = templateGroups.Any(tg => tg.GroupId == existing.GroupId);

                groupLookup.TryGetValue(existing.GroupId, out var name);

                if (!willBeReplaced)
                {
                    groupChanges.Add(new ProjectedGroupChange
                    {
                        GroupId = existing.GroupId,
                        GroupName = name ?? string.Empty,
                        ChangeType = "Remove"
                    });
                }
            }

            foreach (var tg in templateGroups)
            {
                var alreadyExists = existingMemberships.Any(gm => gm.GroupId == tg.GroupId);

                groupLookup.TryGetValue(tg.GroupId, out var name);

                groupChanges.Add(new ProjectedGroupChange
                {
                    GroupId = tg.GroupId,
                    GroupName = name ?? string.Empty,
                    ChangeType = alreadyExists ? "Unchanged" : "Add"
                });
            }
        }
        else
        {
            foreach (var tg in templateGroups)
            {
                var exists = existingMemberships.Any(gm => gm.GroupId == tg.GroupId);

                groupLookup.TryGetValue(tg.GroupId, out var name);

                groupChanges.Add(new ProjectedGroupChange
                {
                    GroupId = tg.GroupId,
                    GroupName = name ?? string.Empty,
                    ChangeType = exists ? "Unchanged" : "Add"
                });
            }
        }

        // Step 5: Compute projected access detail changes
        var accessDetailChanges = new List<ProjectedAccessDetailChange>();

        var existingAccessDetails = await _db.UserAccessDetails
            .IgnoreQueryFilters()
            .Where(ad =>
                ad.TenantId == tenantId &&
                ad.ApplicationId == applicationId &&
                ad.UserProfileId == userProfileId)
            .ToListAsync(ct);

        if (options.ReplaceExistingAccessDetails)
        {
            foreach (var existing in existingAccessDetails)
            {
                var willBeReplaced = templateAccessDetails.Any(tad =>
                    tad.AccessDetailType == existing.AccessDetailType &&
                    tad.AccessDetailCode == existing.AccessDetailCode);

                if (!willBeReplaced)
                {
                    accessDetailChanges.Add(new ProjectedAccessDetailChange
                    {
                        AccessDetailType = existing.AccessDetailType,
                        AccessDetailCode = existing.AccessDetailCode,
                        AccessDetailValue = existing.AccessDetailValue,
                        ChangeType = "Remove"
                    });
                }
            }

            foreach (var tad in templateAccessDetails)
            {
                var alreadyExists = existingAccessDetails.Any(ad =>
                    ad.AccessDetailType == tad.AccessDetailType &&
                    ad.AccessDetailCode == tad.AccessDetailCode);

                accessDetailChanges.Add(new ProjectedAccessDetailChange
                {
                    AccessDetailType = tad.AccessDetailType,
                    AccessDetailCode = tad.AccessDetailCode,
                    AccessDetailValue = tad.AccessDetailValue,
                    ChangeType = alreadyExists ? "Unchanged" : "Add"
                });
            }
        }
        else
        {
            foreach (var tad in templateAccessDetails)
            {
                var exists = existingAccessDetails.Any(ad =>
                    ad.AccessDetailType == tad.AccessDetailType &&
                    ad.AccessDetailCode == tad.AccessDetailCode);

                accessDetailChanges.Add(new ProjectedAccessDetailChange
                {
                    AccessDetailType = tad.AccessDetailType,
                    AccessDetailCode = tad.AccessDetailCode,
                    AccessDetailValue = tad.AccessDetailValue,
                    ChangeType = exists ? "Unchanged" : "Add"
                });
            }
        }

        return new TemplatePreviewResult
        {
            PermissionChanges = permissionChanges,
            GroupChanges = groupChanges,
            AccessDetailChanges = accessDetailChanges
        };
    }

    private async Task InvalidateUserCacheAsync(
        Guid tenantId, Guid applicationId, Guid userProfileId,
        CancellationToken ct)
    {
        // Template application combines user permission + group membership + access detail invalidation
        // Invalidate permission caches
        await _cache.RemoveByPrefixAsync($"perm:{tenantId}:{applicationId}:{userProfileId}:", ct);
        await _cache.RemoveByPrefixAsync($"perm-effective:{tenantId}:{applicationId}:{userProfileId}", ct);

        // Invalidate groups cache (template may add group memberships)
        await _cache.RemoveByPrefixAsync($"groups:{tenantId}:{applicationId}:{userProfileId}", ct);

        // Invalidate access detail cache
        await _cache.RemoveByPrefixAsync($"access:{tenantId}:{applicationId}:{userProfileId}:", ct);
    }
}
