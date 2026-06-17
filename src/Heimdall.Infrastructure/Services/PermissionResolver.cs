using System.Diagnostics;
using Heimdall.Domain.Entities;
using Heimdall.Domain.Enums;
using Heimdall.Domain.Interfaces;
using Heimdall.Domain.Models;
using Heimdall.Infrastructure.Logging;
using Heimdall.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Heimdall.Infrastructure.Services;

public class PermissionResolver : IPermissionResolver
{
    private readonly HeimdallDbContext _db;
    private readonly IHeimdallMetricsService _metrics;

    public PermissionResolver(HeimdallDbContext db, IHeimdallMetricsService metrics)
    {
        _db = db;
        _metrics = metrics;
    }

    public async Task<PermissionCheckResult> CheckPermissionAsync(
        Guid tenantId, Guid applicationId, Guid userProfileId,
        string functionalAreaCode, string permissionTypeCode,
        DateTime? evaluationTime = null, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var result = await CheckPermissionAsyncCore(tenantId, applicationId, userProfileId,
            functionalAreaCode, permissionTypeCode, evaluationTime, ct);
        sw.Stop();
        _metrics.RecordPermissionCheck(result.Allowed, sw.Elapsed.TotalMilliseconds);
        return result;
    }

    private async Task<PermissionCheckResult> CheckPermissionAsyncCore(
        Guid tenantId, Guid applicationId, Guid userProfileId,
        string functionalAreaCode, string permissionTypeCode,
        DateTime? evaluationTime = null, CancellationToken ct = default)
    {
        var evalTime = evaluationTime ?? DateTime.UtcNow;

        // Step 1: Validate user status
        var user = await _db.UserProfiles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == userProfileId && u.TenantId == tenantId, ct);

        if (user is null)
            return DenyResult("Deny", "User not found");

        if (user.Status != UserStatus.Active)
            return DenyResult("Deny", "User is inactive");

        // Step 2: Resolve permission by FunctionalAreaCode + PermissionTypeCode
        var functionalArea = await _db.FunctionalAreas
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(fa =>
                fa.TenantId == tenantId &&
                fa.ApplicationId == applicationId &&
                fa.FunctionalAreaCode == functionalAreaCode, ct);

        if (functionalArea is null)
            return DenyResult("Deny", "Functional area not found");

        var permissionType = await _db.PermissionTypes
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(pt =>
                pt.TenantId == tenantId &&
                pt.ApplicationId == applicationId &&
                pt.Code == permissionTypeCode, ct);

        if (permissionType is null)
            return DenyResult("Deny", "Permission type not found");

        var permission = await _db.Permissions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p =>
                p.TenantId == tenantId &&
                p.ApplicationId == applicationId &&
                p.FunctionalAreaId == functionalArea.Id &&
                p.PermissionTypeId == permissionType.Id, ct);

        if (permission is null)
            return DenyResult("Deny", "Permission not found");

        // Step 3: Validate referenced entities are active
        if (!functionalArea.IsActive)
            return DenyResult("Deny", "Functional area is inactive");

        if (!permissionType.IsActive)
            return DenyResult("Deny", "Permission type is inactive");

        if (!permission.IsActive)
            return DenyResult("Deny", "Permission is inactive");

        // Steps 4-8: Evaluate assignments
        return await EvaluateAssignmentsAsync(tenantId, applicationId, userProfileId, permission.Id, evalTime, ct);
    }

    public async Task<PermissionCheckResult> CheckPermissionByCodeAsync(
        Guid tenantId, Guid applicationId, Guid userProfileId,
        string permissionCode, DateTime? evaluationTime = null,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var result = await CheckPermissionByCodeAsyncCore(tenantId, applicationId, userProfileId,
            permissionCode, evaluationTime, ct);
        sw.Stop();
        _metrics.RecordPermissionCheck(result.Allowed, sw.Elapsed.TotalMilliseconds);
        return result;
    }

    private async Task<PermissionCheckResult> CheckPermissionByCodeAsyncCore(
        Guid tenantId, Guid applicationId, Guid userProfileId,
        string permissionCode, DateTime? evaluationTime = null,
        CancellationToken ct = default)
    {
        var evalTime = evaluationTime ?? DateTime.UtcNow;

        // Step 1: Validate user status
        var user = await _db.UserProfiles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == userProfileId && u.TenantId == tenantId, ct);

        if (user is null)
            return DenyResult("Deny", "User not found");

        if (user.Status != UserStatus.Active)
            return DenyResult("Deny", "User is inactive");

        // Step 2: Resolve permission by PermissionCode
        var permission = await _db.Permissions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p =>
                p.TenantId == tenantId &&
                p.ApplicationId == applicationId &&
                p.PermissionCode == permissionCode, ct);

        if (permission is null)
            return DenyResult("Deny", "Permission not found");

        // Step 3: Validate referenced entities are active
        var functionalArea = await _db.FunctionalAreas
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(fa => fa.Id == permission.FunctionalAreaId, ct);

        if (functionalArea is null || !functionalArea.IsActive)
            return DenyResult("Deny", "Functional area is inactive");

        var permissionType = await _db.PermissionTypes
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(pt => pt.Id == permission.PermissionTypeId, ct);

        if (permissionType is null || !permissionType.IsActive)
            return DenyResult("Deny", "Permission type is inactive");

        if (!permission.IsActive)
            return DenyResult("Deny", "Permission is inactive");

        // Steps 4-8: Evaluate assignments
        return await EvaluateAssignmentsAsync(tenantId, applicationId, userProfileId, permission.Id, evalTime, ct);
    }

    public async Task<PermissionCheckResult> CheckPermissionByExternalIdAsync(
        Guid tenantId, Guid applicationId,
        string externalSubjectId, string identityProvider,
        string permissionCode, DateTime? evaluationTime = null,
        CancellationToken ct = default)
    {
        // Resolve UserProfile by (TenantId, ExternalSubjectId, IdentityProvider)
        var userProfile = await _db.UserProfiles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u =>
                u.TenantId == tenantId &&
                u.ExternalSubjectId == externalSubjectId &&
                u.IdentityProvider == identityProvider, ct);

        if (userProfile is null)
            return DenyResult("Deny", "User not found for external subject ID");

        // Delegate to standard code-based resolution
        return await CheckPermissionByCodeAsync(tenantId, applicationId, userProfile.Id, permissionCode, evaluationTime, ct);
    }

    public async Task<BatchPermissionCheckResult> CheckBatchAsync(
        Guid tenantId, Guid applicationId, Guid userProfileId,
        IReadOnlyList<PermissionCheckRequest> checks,
        DateTime? evaluationTime = null, CancellationToken ct = default)
    {
        var evalTime = evaluationTime ?? DateTime.UtcNow;

        // Enforce max 50 batch size
        if (checks.Count > 50)
            throw new ArgumentException("Batch permission check is limited to 50 items.");

        // Shared: single round-trip for user validation
        var user = await _db.UserProfiles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == userProfileId && u.TenantId == tenantId, ct);

        if (user is null)
        {
            var denyAll = checks.Select(_ => DenyResult("Deny", "User not found")).ToList();
            return new BatchPermissionCheckResult { Results = denyAll };
        }

        if (user.Status != UserStatus.Active)
        {
            var denyAll = checks.Select(_ => DenyResult("Deny", "User is inactive")).ToList();
            return new BatchPermissionCheckResult { Results = denyAll };
        }

        // Shared: single round-trip for group membership lookup
        var userGroupIds = await _db.GroupMemberships
            .IgnoreQueryFilters()
            .Where(gm => gm.TenantId == tenantId && gm.ApplicationId == applicationId && gm.UserProfileId == userProfileId)
            .Select(gm => gm.GroupId)
            .ToListAsync(ct);

        // Get active groups for filtering
        var activeGroupIds = await _db.Groups
            .IgnoreQueryFilters()
            .Where(g => g.TenantId == tenantId && g.ApplicationId == applicationId && g.IsActive && userGroupIds.Contains(g.Id))
            .Select(g => g.Id)
            .ToListAsync(ct);

        // Pre-load active groups with names for building matched assignments
        var activeGroups = await _db.Groups
            .IgnoreQueryFilters()
            .Where(g => g.TenantId == tenantId && g.ApplicationId == applicationId && g.IsActive && userGroupIds.Contains(g.Id))
            .ToDictionaryAsync(g => g.Id, g => g.Name, ct);

        var results = new List<PermissionCheckResult>(checks.Count);

        foreach (var check in checks)
        {
            var result = await EvaluateSingleCheckAsync(
                tenantId, applicationId, userProfileId, check, evalTime, activeGroupIds, activeGroups, ct);
            results.Add(result);
        }

        return new BatchPermissionCheckResult { Results = results };
    }

    public async Task<EffectivePermissionsResult> GetEffectivePermissionsAsync(
        Guid tenantId, Guid applicationId, Guid userProfileId,
        DateTime? evaluationTime = null, CancellationToken ct = default)
    {
        var evalTime = evaluationTime ?? DateTime.UtcNow;

        // Validate user
        var user = await _db.UserProfiles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == userProfileId && u.TenantId == tenantId, ct);

        if (user is null || user.Status != UserStatus.Active)
            return new EffectivePermissionsResult { Permissions = [] };

        // Get all active permissions for this application
        var permissions = await _db.Permissions
            .IgnoreQueryFilters()
            .Where(p => p.TenantId == tenantId && p.ApplicationId == applicationId && p.IsActive)
            .ToListAsync(ct);

        // Get active functional areas and permission types for filtering
        var activeFunctionalAreaIds = await _db.FunctionalAreas
            .IgnoreQueryFilters()
            .Where(fa => fa.TenantId == tenantId && fa.ApplicationId == applicationId && fa.IsActive)
            .Select(fa => fa.Id)
            .ToListAsync(ct);

        var activePermissionTypeIds = await _db.PermissionTypes
            .IgnoreQueryFilters()
            .Where(pt => pt.TenantId == tenantId && pt.ApplicationId == applicationId && pt.IsActive)
            .Select(pt => pt.Id)
            .ToListAsync(ct);

        // Filter to only permissions with active FA and PT
        var validPermissions = permissions
            .Where(p => activeFunctionalAreaIds.Contains(p.FunctionalAreaId) && activePermissionTypeIds.Contains(p.PermissionTypeId))
            .ToList();

        // Get user's group memberships (active groups only)
        var userGroupIds = await _db.GroupMemberships
            .IgnoreQueryFilters()
            .Where(gm => gm.TenantId == tenantId && gm.ApplicationId == applicationId && gm.UserProfileId == userProfileId)
            .Select(gm => gm.GroupId)
            .ToListAsync(ct);

        var activeGroupIds = await _db.Groups
            .IgnoreQueryFilters()
            .Where(g => g.TenantId == tenantId && g.ApplicationId == applicationId && g.IsActive && userGroupIds.Contains(g.Id))
            .Select(g => g.Id)
            .ToListAsync(ct);

        // Get all direct assignments for the user
        var directAssignments = await _db.UserPermissionAssignments
            .IgnoreQueryFilters()
            .Where(a => a.TenantId == tenantId && a.ApplicationId == applicationId && a.UserProfileId == userProfileId)
            .ToListAsync(ct);

        // Get all group assignments for user's active groups
        var groupAssignments = activeGroupIds.Count > 0
            ? await _db.GroupPermissionAssignments
                .IgnoreQueryFilters()
                .Where(a => a.TenantId == tenantId && a.ApplicationId == applicationId && activeGroupIds.Contains(a.GroupId))
                .ToListAsync(ct)
            : new List<GroupPermissionAssignment>();

        // Load FA and PT codes for response building
        var functionalAreas = await _db.FunctionalAreas
            .IgnoreQueryFilters()
            .Where(fa => fa.TenantId == tenantId && fa.ApplicationId == applicationId && fa.IsActive)
            .ToDictionaryAsync(fa => fa.Id, fa => fa.FunctionalAreaCode, ct);

        var permissionTypes = await _db.PermissionTypes
            .IgnoreQueryFilters()
            .Where(pt => pt.TenantId == tenantId && pt.ApplicationId == applicationId && pt.IsActive)
            .ToDictionaryAsync(pt => pt.Id, pt => pt.Code, ct);

        var effectivePermissions = new List<EffectivePermission>();

        foreach (var permission in validPermissions)
        {
            // Collect time-valid direct assignments for this permission
            var validDirects = directAssignments
                .Where(a => a.PermissionId == permission.Id && IsWithinTimeWindow(a.ValidFrom, a.ValidTo, evalTime))
                .ToList();

            // Collect time-valid group assignments for this permission
            var validGroups = groupAssignments
                .Where(a => a.PermissionId == permission.Id && IsWithinTimeWindow(a.ValidFrom, a.ValidTo, evalTime))
                .ToList();

            if (validDirects.Count == 0 && validGroups.Count == 0)
                continue;

            // Deny overrides allow
            var hasDeny = validDirects.Any(a => a.Effect == Effect.Deny) ||
                          validGroups.Any(a => a.Effect == Effect.Deny);

            var effect = hasDeny ? "Deny" : "Allow";

            functionalAreas.TryGetValue(permission.FunctionalAreaId, out var faCode);
            permissionTypes.TryGetValue(permission.PermissionTypeId, out var ptCode);

            effectivePermissions.Add(new EffectivePermission
            {
                PermissionId = permission.Id,
                PermissionCode = permission.PermissionCode,
                FunctionalAreaCode = faCode ?? string.Empty,
                PermissionTypeCode = ptCode ?? string.Empty,
                Effect = effect
            });
        }

        return new EffectivePermissionsResult { Permissions = effectivePermissions };
    }

    #region Private Helpers

    private async Task<PermissionCheckResult> EvaluateAssignmentsAsync(
        Guid tenantId, Guid applicationId, Guid userProfileId,
        Guid permissionId, DateTime evalTime, CancellationToken ct)
    {
        // Step 4: Collect direct assignments
        var directAssignments = await _db.UserPermissionAssignments
            .IgnoreQueryFilters()
            .Where(a =>
                a.TenantId == tenantId &&
                a.ApplicationId == applicationId &&
                a.UserProfileId == userProfileId &&
                a.PermissionId == permissionId)
            .ToListAsync(ct);

        // Step 5: Collect group assignments from active groups
        var userGroupIds = await _db.GroupMemberships
            .IgnoreQueryFilters()
            .Where(gm => gm.TenantId == tenantId && gm.ApplicationId == applicationId && gm.UserProfileId == userProfileId)
            .Select(gm => gm.GroupId)
            .ToListAsync(ct);

        var activeGroups = await _db.Groups
            .IgnoreQueryFilters()
            .Where(g => g.TenantId == tenantId && g.ApplicationId == applicationId && g.IsActive && userGroupIds.Contains(g.Id))
            .ToDictionaryAsync(g => g.Id, g => g.Name, ct);

        var activeGroupIds = activeGroups.Keys.ToList();

        var groupAssignments = activeGroupIds.Count > 0
            ? await _db.GroupPermissionAssignments
                .IgnoreQueryFilters()
                .Where(a =>
                    a.TenantId == tenantId &&
                    a.ApplicationId == applicationId &&
                    a.PermissionId == permissionId &&
                    activeGroupIds.Contains(a.GroupId))
                .ToListAsync(ct)
            : new List<GroupPermissionAssignment>();

        // Step 6: Filter by time window
        var validDirectAssignments = directAssignments
            .Where(a => IsWithinTimeWindow(a.ValidFrom, a.ValidTo, evalTime))
            .ToList();

        var validGroupAssignments = groupAssignments
            .Where(a => IsWithinTimeWindow(a.ValidFrom, a.ValidTo, evalTime))
            .ToList();

        // No valid assignments => Deny
        if (validDirectAssignments.Count == 0 && validGroupAssignments.Count == 0)
            return DenyResult("Deny", "No applicable assignments");

        // Step 7: Apply deny-overrides-allow
        var hasDeny = validDirectAssignments.Any(a => a.Effect == Effect.Deny) ||
                      validGroupAssignments.Any(a => a.Effect == Effect.Deny);

        // Step 8: Build response with matched assignments
        var matchedAssignments = new List<MatchedAssignment>();

        foreach (var a in validDirectAssignments)
        {
            matchedAssignments.Add(new MatchedAssignment
            {
                AssignmentId = a.Id,
                Source = "DirectUser",
                Effect = a.Effect.ToString(),
                GroupId = null,
                GroupName = null
            });
        }

        foreach (var a in validGroupAssignments)
        {
            activeGroups.TryGetValue(a.GroupId, out var groupName);
            matchedAssignments.Add(new MatchedAssignment
            {
                AssignmentId = a.Id,
                Source = "Group",
                Effect = a.Effect.ToString(),
                GroupId = a.GroupId,
                GroupName = groupName
            });
        }

        if (hasDeny)
            return new PermissionCheckResult
            {
                Allowed = false,
                Decision = "Deny",
                Reason = "Explicit deny assignment exists",
                MatchedAssignments = matchedAssignments
            };

        return new PermissionCheckResult
        {
            Allowed = true,
            Decision = "Allow",
            Reason = "Permission granted",
            MatchedAssignments = matchedAssignments
        };
    }

    private async Task<PermissionCheckResult> EvaluateSingleCheckAsync(
        Guid tenantId, Guid applicationId, Guid userProfileId,
        PermissionCheckRequest check, DateTime evalTime,
        List<Guid> activeGroupIds, Dictionary<Guid, string> activeGroups,
        CancellationToken ct)
    {
        // Resolve the permission
        Permission? permission = null;

        if (!string.IsNullOrEmpty(check.PermissionCode))
        {
            permission = await _db.Permissions
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(p =>
                    p.TenantId == tenantId &&
                    p.ApplicationId == applicationId &&
                    p.PermissionCode == check.PermissionCode, ct);
        }
        else if (!string.IsNullOrEmpty(check.FunctionalAreaCode) && !string.IsNullOrEmpty(check.PermissionTypeCode))
        {
            var fa = await _db.FunctionalAreas
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(f =>
                    f.TenantId == tenantId &&
                    f.ApplicationId == applicationId &&
                    f.FunctionalAreaCode == check.FunctionalAreaCode, ct);

            if (fa is null)
                return DenyResult("Deny", "Functional area not found");

            if (!fa.IsActive)
                return DenyResult("Deny", "Functional area is inactive");

            var pt = await _db.PermissionTypes
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(p =>
                    p.TenantId == tenantId &&
                    p.ApplicationId == applicationId &&
                    p.Code == check.PermissionTypeCode, ct);

            if (pt is null)
                return DenyResult("Deny", "Permission type not found");

            if (!pt.IsActive)
                return DenyResult("Deny", "Permission type is inactive");

            permission = await _db.Permissions
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(p =>
                    p.TenantId == tenantId &&
                    p.ApplicationId == applicationId &&
                    p.FunctionalAreaId == fa.Id &&
                    p.PermissionTypeId == pt.Id, ct);
        }

        if (permission is null)
            return DenyResult("Deny", "Permission not found");

        if (!permission.IsActive)
            return DenyResult("Deny", "Permission is inactive");

        // Validate FA and PT active when resolved by code
        if (!string.IsNullOrEmpty(check.PermissionCode))
        {
            var fa = await _db.FunctionalAreas
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(f => f.Id == permission.FunctionalAreaId, ct);

            if (fa is null || !fa.IsActive)
                return DenyResult("Deny", "Functional area is inactive");

            var pt = await _db.PermissionTypes
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.Id == permission.PermissionTypeId, ct);

            if (pt is null || !pt.IsActive)
                return DenyResult("Deny", "Permission type is inactive");
        }

        // Collect direct assignments
        var directAssignments = await _db.UserPermissionAssignments
            .IgnoreQueryFilters()
            .Where(a =>
                a.TenantId == tenantId &&
                a.ApplicationId == applicationId &&
                a.UserProfileId == userProfileId &&
                a.PermissionId == permission.Id)
            .ToListAsync(ct);

        // Collect group assignments from shared active groups
        var groupAssignments = activeGroupIds.Count > 0
            ? await _db.GroupPermissionAssignments
                .IgnoreQueryFilters()
                .Where(a =>
                    a.TenantId == tenantId &&
                    a.ApplicationId == applicationId &&
                    a.PermissionId == permission.Id &&
                    activeGroupIds.Contains(a.GroupId))
                .ToListAsync(ct)
            : new List<GroupPermissionAssignment>();

        // Filter by time window
        var validDirects = directAssignments
            .Where(a => IsWithinTimeWindow(a.ValidFrom, a.ValidTo, evalTime))
            .ToList();

        var validGroups = groupAssignments
            .Where(a => IsWithinTimeWindow(a.ValidFrom, a.ValidTo, evalTime))
            .ToList();

        if (validDirects.Count == 0 && validGroups.Count == 0)
            return DenyResult("Deny", "No applicable assignments");

        // Deny overrides allow
        var hasDeny = validDirects.Any(a => a.Effect == Effect.Deny) ||
                      validGroups.Any(a => a.Effect == Effect.Deny);

        // Build matched assignments
        var matched = new List<MatchedAssignment>();

        foreach (var a in validDirects)
        {
            matched.Add(new MatchedAssignment
            {
                AssignmentId = a.Id,
                Source = "DirectUser",
                Effect = a.Effect.ToString(),
                GroupId = null,
                GroupName = null
            });
        }

        foreach (var a in validGroups)
        {
            activeGroups.TryGetValue(a.GroupId, out var groupName);
            matched.Add(new MatchedAssignment
            {
                AssignmentId = a.Id,
                Source = "Group",
                Effect = a.Effect.ToString(),
                GroupId = a.GroupId,
                GroupName = groupName
            });
        }

        if (hasDeny)
            return new PermissionCheckResult
            {
                Allowed = false,
                Decision = "Deny",
                Reason = "Explicit deny assignment exists",
                MatchedAssignments = matched
            };

        return new PermissionCheckResult
        {
            Allowed = true,
            Decision = "Allow",
            Reason = "Permission granted",
            MatchedAssignments = matched
        };
    }

    private static bool IsWithinTimeWindow(DateTime? validFrom, DateTime? validTo, DateTime evaluationTime)
    {
        if (validFrom.HasValue && evaluationTime < validFrom.Value)
            return false;

        if (validTo.HasValue && evaluationTime > validTo.Value)
            return false;

        return true;
    }

    private static PermissionCheckResult DenyResult(string decision, string reason)
    {
        return new PermissionCheckResult
        {
            Allowed = false,
            Decision = decision,
            Reason = reason,
            MatchedAssignments = []
        };
    }

    #endregion
}
