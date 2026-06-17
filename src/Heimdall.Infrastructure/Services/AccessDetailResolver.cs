using Heimdall.Domain.Entities;
using Heimdall.Domain.Enums;
using Heimdall.Domain.Interfaces;
using Heimdall.Domain.Models;
using Heimdall.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Heimdall.Infrastructure.Services;

public class AccessDetailResolver : IAccessDetailResolver
{
    private readonly HeimdallDbContext _db;

    public AccessDetailResolver(HeimdallDbContext db)
    {
        _db = db;
    }

    public async Task<AccessDetailLookupResult> GetAccessDetailsAsync(
        Guid tenantId, Guid applicationId, Guid userProfileId,
        string functionalAreaCode, DateTime? evaluationTime = null,
        CancellationToken ct = default)
    {
        var now = evaluationTime ?? DateTime.UtcNow;

        // Step 1: Look up the FunctionalArea by (ApplicationId, FunctionalAreaCode)
        var functionalArea = await _db.FunctionalAreas
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(fa =>
                fa.TenantId == tenantId &&
                fa.ApplicationId == applicationId &&
                fa.FunctionalAreaCode == functionalAreaCode &&
                fa.IsActive,
                ct);

        if (functionalArea is null)
        {
            return new AccessDetailLookupResult { Details = [] };
        }

        // Step 2: Validate user has functional permission to the area
        var user = await _db.UserProfiles
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userProfileId && u.TenantId == tenantId, ct);

        if (user is null || user.Status == UserStatus.Inactive)
        {
            return new AccessDetailLookupResult { Details = [] };
        }

        var hasPermission = await HasFunctionalAreaPermissionAsync(
            tenantId, applicationId, userProfileId, functionalArea.Id, now, ct);

        if (!hasPermission)
        {
            return new AccessDetailLookupResult { Details = [] };
        }

        // Step 3: Get active FunctionalAreaAccessRequirements for the FunctionalArea
        var accessRequirements = await _db.FunctionalAreaAccessRequirements
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(r =>
                r.TenantId == tenantId &&
                r.FunctionalAreaId == functionalArea.Id &&
                r.IsActive)
            .ToListAsync(ct);

        // Step 4: If no requirements exist, return empty (access based solely on permission)
        if (accessRequirements.Count == 0)
        {
            return new AccessDetailLookupResult { Details = [] };
        }

        // Step 5: Get relevant AccessDetailTypes from the requirements
        var relevantTypes = accessRequirements
            .Select(r => r.AccessDetailType)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Step 6: Collect direct UserAccessDetail records (active, non-expired, matching types)
        var directDetails = await _db.UserAccessDetails
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(d =>
                d.TenantId == tenantId &&
                d.UserProfileId == userProfileId &&
                d.ApplicationId == applicationId &&
                d.IsActive &&
                (d.ValidFrom == null || d.ValidFrom <= now) &&
                (d.ValidTo == null || d.ValidTo >= now))
            .ToListAsync(ct);

        // Filter by relevant types in memory (EF can't translate HashSet.Contains with StringComparer)
        var filteredDirectDetails = directDetails
            .Where(d => relevantTypes.Contains(d.AccessDetailType))
            .ToList();

        // Step 7: Get active groups the user belongs to and collect GroupAccessDetail records
        var activeGroups = await _db.GroupMemberships
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(gm =>
                gm.TenantId == tenantId &&
                gm.UserProfileId == userProfileId &&
                gm.ApplicationId == applicationId)
            .Join(
                _db.Groups.IgnoreQueryFilters().AsNoTracking()
                    .Where(g => g.TenantId == tenantId && g.IsActive),
                gm => gm.GroupId,
                g => g.Id,
                (gm, g) => new { g.Id, g.Name })
            .ToListAsync(ct);

        var groupIds = activeGroups.Select(g => g.Id).ToList();
        var groupNameLookup = activeGroups.ToDictionary(g => g.Id, g => g.Name);

        List<GroupAccessDetail> filteredGroupDetails = [];

        if (groupIds.Count > 0)
        {
            var groupDetails = await _db.GroupAccessDetails
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(d =>
                    d.TenantId == tenantId &&
                    groupIds.Contains(d.GroupId) &&
                    d.ApplicationId == applicationId &&
                    d.IsActive &&
                    (d.ValidFrom == null || d.ValidFrom <= now) &&
                    (d.ValidTo == null || d.ValidTo >= now))
                .ToListAsync(ct);

            filteredGroupDetails = groupDetails
                .Where(d => relevantTypes.Contains(d.AccessDetailType))
                .ToList();
        }

        // Step 8: Build AccessDetailEntry records with source indicators
        var entries = new List<AccessDetailEntry>();

        foreach (var detail in filteredDirectDetails)
        {
            entries.Add(new AccessDetailEntry
            {
                AccessDetailId = detail.Id,
                AccessDetailType = detail.AccessDetailType,
                AccessDetailCode = detail.AccessDetailCode,
                AccessDetailValue = detail.AccessDetailValue,
                Description = detail.Description,
                Source = "DirectUser",
                GroupId = null,
                GroupName = null
            });
        }

        foreach (var detail in filteredGroupDetails)
        {
            entries.Add(new AccessDetailEntry
            {
                AccessDetailId = detail.Id,
                AccessDetailType = detail.AccessDetailType,
                AccessDetailCode = detail.AccessDetailCode,
                AccessDetailValue = detail.AccessDetailValue,
                Description = detail.Description,
                Source = "Group",
                GroupId = detail.GroupId,
                GroupName = groupNameLookup.GetValueOrDefault(detail.GroupId)
            });
        }

        // Step 9: Return combined results
        return new AccessDetailLookupResult { Details = entries };
    }

    private async Task<bool> HasFunctionalAreaPermissionAsync(
        Guid tenantId, Guid applicationId, Guid userProfileId,
        Guid functionalAreaId, DateTime now, CancellationToken ct)
    {
        // Check direct user permission assignments for the functional area
        var hasDirectPermission = await _db.UserPermissionAssignments
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Join(
                _db.Permissions.IgnoreQueryFilters()
                    .Where(p => p.TenantId == tenantId &&
                                p.ApplicationId == applicationId &&
                                p.FunctionalAreaId == functionalAreaId &&
                                p.IsActive),
                upa => upa.PermissionId,
                p => p.Id,
                (upa, p) => upa)
            .AnyAsync(upa =>
                upa.TenantId == tenantId &&
                upa.ApplicationId == applicationId &&
                upa.UserProfileId == userProfileId &&
                upa.Effect == Effect.Allow &&
                (upa.ValidFrom == null || upa.ValidFrom <= now) &&
                (upa.ValidTo == null || upa.ValidTo >= now),
                ct);

        if (hasDirectPermission)
            return true;

        // Check group permission assignments for the functional area
        var userGroupIds = await _db.GroupMemberships
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(gm =>
                gm.TenantId == tenantId &&
                gm.UserProfileId == userProfileId &&
                gm.ApplicationId == applicationId)
            .Join(
                _db.Groups.IgnoreQueryFilters()
                    .Where(g => g.TenantId == tenantId && g.IsActive),
                gm => gm.GroupId,
                g => g.Id,
                (gm, g) => gm.GroupId)
            .ToListAsync(ct);

        if (userGroupIds.Count == 0)
            return false;

        var hasGroupPermission = await _db.GroupPermissionAssignments
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Join(
                _db.Permissions.IgnoreQueryFilters()
                    .Where(p => p.TenantId == tenantId &&
                                p.ApplicationId == applicationId &&
                                p.FunctionalAreaId == functionalAreaId &&
                                p.IsActive),
                gpa => gpa.PermissionId,
                p => p.Id,
                (gpa, p) => gpa)
            .AnyAsync(gpa =>
                gpa.TenantId == tenantId &&
                gpa.ApplicationId == applicationId &&
                userGroupIds.Contains(gpa.GroupId) &&
                gpa.Effect == Effect.Allow &&
                (gpa.ValidFrom == null || gpa.ValidFrom <= now) &&
                (gpa.ValidTo == null || gpa.ValidTo >= now),
                ct);

        return hasGroupPermission;
    }
}
