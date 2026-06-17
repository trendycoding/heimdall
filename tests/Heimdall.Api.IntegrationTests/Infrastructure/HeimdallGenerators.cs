using FsCheck;
using FsCheck.Fluent;
using Heimdall.Domain.Entities;
using Heimdall.Domain.Enums;
using ApplicationEntity = Heimdall.Domain.Entities.Application;

namespace Heimdall.Api.IntegrationTests.Infrastructure;

/// <summary>
/// FsCheck custom generators for Heimdall domain entities.
/// Generates valid entities that satisfy all domain constraints (max lengths, 
/// character patterns, required fields, etc.) for use in property-based tests.
/// </summary>
public static class HeimdallGenerators
{
    // Character pools for valid code generation
    private static readonly char[] UpperAlphaNumUnderscoreChars =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_".ToCharArray();
    private static readonly char[] AlphaNumUnderscoreChars =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_".ToCharArray();
    private static readonly char[] SlugMiddleChars =
        "abcdefghijklmnopqrstuvwxyz0123456789-".ToCharArray();
    private static readonly char[] AlphaNumChars =
        "abcdefghijklmnopqrstuvwxyz0123456789".ToCharArray();
    private static readonly char[] NameChars =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 -".ToCharArray();

    /// <summary>
    /// Generates a non-empty string from a given character set with constrained length.
    /// </summary>
    private static Gen<string> GenStringFromChars(char[] chars, int minLength, int maxLength)
    {
        return from length in Gen.Choose(minLength, maxLength)
               from arr in Gen.ArrayOf(Gen.Elements(chars), length)
               select new string(arr);
    }

    /// <summary>
    /// Generates a valid slug: 1-64 lowercase alphanumeric or hyphens,
    /// starting and ending with alphanumeric.
    /// </summary>
    public static Gen<string> GenSlug()
    {
        return from length in Gen.Choose(1, 64)
               from first in Gen.Elements(AlphaNumChars)
               from last in Gen.Elements(AlphaNumChars)
               from middle in Gen.ArrayOf(Gen.Elements(SlugMiddleChars), Math.Max(0, length - 2))
               select length == 1
                   ? first.ToString()
                   : length == 2
                       ? $"{first}{last}"
                       : $"{first}{new string(middle)}{last}";
    }

    /// <summary>
    /// Generates a valid FunctionalAreaCode: max 50 chars, uppercase alphanumeric + underscore.
    /// </summary>
    public static Gen<string> GenFunctionalAreaCode()
    {
        return GenStringFromChars(UpperAlphaNumUnderscoreChars, 1, 50);
    }

    /// <summary>
    /// Generates a valid PermissionTypeCode: max 100 chars, alphanumeric + underscore.
    /// </summary>
    public static Gen<string> GenPermissionTypeCode()
    {
        return GenStringFromChars(AlphaNumUnderscoreChars, 1, 100);
    }

    /// <summary>
    /// Generates a valid PermissionCode: max 200 chars, uppercase alphanumeric + underscore.
    /// </summary>
    public static Gen<string> GenPermissionCode()
    {
        return GenStringFromChars(UpperAlphaNumUnderscoreChars, 1, 200);
    }

    /// <summary>
    /// Generates a valid name with realistic length constraints.
    /// </summary>
    public static Gen<string> GenName(int maxLength = 128)
    {
        var effectiveMax = Math.Min(maxLength, 50);
        return from length in Gen.Choose(1, effectiveMax)
               from arr in Gen.ArrayOf(Gen.Elements(NameChars), length)
               let raw = new string(arr).Trim()
               where raw.Length > 0
               select raw;
    }

    /// <summary>
    /// Generates a valid email address.
    /// </summary>
    public static Gen<string> GenEmail()
    {
        return from local in GenStringFromChars(AlphaNumChars, 3, 15)
               from domain in Gen.Elements("example.com", "test.dev", "heimdall.io", "corp.net")
               select $"{local}@{domain}";
    }

    /// <summary>
    /// Generates a valid ClientIdentifier: max 128 chars.
    /// </summary>
    public static Gen<string> GenClientIdentifier()
    {
        return GenStringFromChars(AlphaNumUnderscoreChars, 1, 128);
    }

    /// <summary>
    /// Generates a valid Tenant entity with all constraints satisfied.
    /// </summary>
    public static Gen<Tenant> GenTenant()
    {
        return from name in GenName(128)
               from slug in GenSlug()
               from mode in Gen.Elements(
                   PrimaryIdentityMode.EntraExternalId,
                   PrimaryIdentityMode.AzureAdB2C,
                   PrimaryIdentityMode.EntraWorkforce,
                   PrimaryIdentityMode.ExternalOidc,
                   PrimaryIdentityMode.ExternalSaml)
               select new Tenant
               {
                   Name = name,
                   Slug = slug,
                   PrimaryIdentityMode = mode,
                   Status = TenantStatus.Active,
                   CreatedAt = DateTime.UtcNow,
                   CreatedBy = "test@heimdall.dev"
               };
    }

    /// <summary>
    /// Generates a valid Application entity scoped to a given tenant.
    /// </summary>
    public static Gen<ApplicationEntity> GenApplication(Guid tenantId)
    {
        return from name in GenName(200)
               from clientId in GenClientIdentifier()
               select new ApplicationEntity
               {
                   TenantId = tenantId,
                   Name = name,
                   ClientIdentifier = clientId,
                   Status = ApplicationStatus.Active,
                   AllowedRedirectUris = new List<string>(),
                   AllowedOrigins = new List<string>(),
                   CreatedAt = DateTime.UtcNow,
                   CreatedBy = "test@heimdall.dev"
               };
    }

    /// <summary>
    /// Generates a valid UserProfile entity scoped to a given tenant.
    /// </summary>
    public static Gen<UserProfile> GenUserProfile(Guid tenantId)
    {
        return from email in GenEmail()
               from displayName in GenName(100)
               select new UserProfile
               {
                   TenantId = tenantId,
                   ExternalSubjectId = $"ext-{Guid.NewGuid()}",
                   IdentityProvider = "TestProvider",
                   Email = email,
                   DisplayName = displayName,
                   Status = UserStatus.Active,
                   CreatedAt = DateTime.UtcNow,
                   CreatedBy = "test@heimdall.dev"
               };
    }

    /// <summary>
    /// Generates a valid FunctionalArea scoped to a given tenant and application.
    /// </summary>
    public static Gen<FunctionalArea> GenFunctionalArea(Guid tenantId, Guid applicationId)
    {
        return from code in GenFunctionalAreaCode()
               from name in GenName(200)
               select new FunctionalArea
               {
                   TenantId = tenantId,
                   ApplicationId = applicationId,
                   FunctionalAreaCode = code,
                   Name = name,
                   IsActive = true,
                   CreatedAt = DateTime.UtcNow,
                   CreatedBy = "test@heimdall.dev"
               };
    }

    /// <summary>
    /// Generates a valid PermissionType scoped to a given tenant and application.
    /// </summary>
    public static Gen<PermissionType> GenPermissionType(Guid tenantId, Guid applicationId)
    {
        return from code in GenPermissionTypeCode()
               from name in GenName(200)
               select new PermissionType
               {
                   TenantId = tenantId,
                   ApplicationId = applicationId,
                   Code = code,
                   Name = name,
                   IsSystemReserved = false,
                   IsActive = true,
                   CreatedAt = DateTime.UtcNow,
                   CreatedBy = "test@heimdall.dev"
               };
    }

    /// <summary>
    /// Generates a valid Permission scoped to a given tenant, application,
    /// functional area, and permission type.
    /// </summary>
    public static Gen<Permission> GenPermission(
        Guid tenantId, Guid applicationId, Guid functionalAreaId, Guid permissionTypeId)
    {
        return from code in GenPermissionCode()
               from name in GenName(200)
               select new Permission
               {
                   TenantId = tenantId,
                   ApplicationId = applicationId,
                   FunctionalAreaId = functionalAreaId,
                   PermissionTypeId = permissionTypeId,
                   PermissionCode = code,
                   Name = name,
                   IsActive = true,
                   CreatedAt = DateTime.UtcNow,
                   CreatedBy = "test@heimdall.dev"
               };
    }

    /// <summary>
    /// Generates a valid Group scoped to a given tenant and application.
    /// </summary>
    public static Gen<Group> GenGroup(Guid tenantId, Guid applicationId)
    {
        return from name in GenName(200)
               select new Group
               {
                   TenantId = tenantId,
                   ApplicationId = applicationId,
                   Name = name,
                   IsActive = true,
                   CreatedAt = DateTime.UtcNow,
                   CreatedBy = "test@heimdall.dev"
               };
    }

    /// <summary>
    /// Generates a valid UserPermissionAssignment with configurable effect and time window.
    /// </summary>
    public static Gen<UserPermissionAssignment> GenUserPermissionAssignment(
        Guid tenantId, Guid applicationId, Guid userProfileId, Guid permissionId)
    {
        return from effect in Gen.Elements(Effect.Allow, Effect.Deny)
               from window in GenValidityWindow()
               select new UserPermissionAssignment
               {
                   TenantId = tenantId,
                   ApplicationId = applicationId,
                   UserProfileId = userProfileId,
                   PermissionId = permissionId,
                   Effect = effect,
                   ValidFrom = window.ValidFrom,
                   ValidTo = window.ValidTo,
                   CreatedAt = DateTime.UtcNow,
                   CreatedBy = "test@heimdall.dev"
               };
    }

    /// <summary>
    /// Generates a valid GroupPermissionAssignment with configurable effect and time window.
    /// </summary>
    public static Gen<GroupPermissionAssignment> GenGroupPermissionAssignment(
        Guid tenantId, Guid applicationId, Guid groupId, Guid permissionId)
    {
        return from effect in Gen.Elements(Effect.Allow, Effect.Deny)
               from window in GenValidityWindow()
               select new GroupPermissionAssignment
               {
                   TenantId = tenantId,
                   ApplicationId = applicationId,
                   GroupId = groupId,
                   PermissionId = permissionId,
                   Effect = effect,
                   ValidFrom = window.ValidFrom,
                   ValidTo = window.ValidTo,
                   CreatedAt = DateTime.UtcNow,
                   CreatedBy = "test@heimdall.dev"
               };
    }

    /// <summary>
    /// Generates a valid GroupMembership linking a user to a group.
    /// </summary>
    public static Gen<GroupMembership> GenGroupMembership(
        Guid tenantId, Guid applicationId, Guid groupId, Guid userProfileId)
    {
        return Gen.Constant(new GroupMembership
        {
            TenantId = tenantId,
            ApplicationId = applicationId,
            GroupId = groupId,
            UserProfileId = userProfileId,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test@heimdall.dev"
        });
    }

    /// <summary>
    /// Generates a valid temporal window where ValidFrom &lt;= ValidTo.
    /// Returns (ValidFrom, ValidTo) with possible nulls for unbounded ranges.
    /// </summary>
    public static Gen<ValidityWindow> GenValidityWindow()
    {
        return Gen.OneOf(
            // Both null (unbounded)
            Gen.Constant(new ValidityWindow(null, null)),
            // Only ValidFrom set (valid from date, no end)
            from days in Gen.Choose(-365, 365)
            select new ValidityWindow(DateTime.UtcNow.AddDays(days), null),
            // Only ValidTo set (no start, valid until date)
            from days in Gen.Choose(-365, 365)
            select new ValidityWindow(null, DateTime.UtcNow.AddDays(days)),
            // Both set (ValidFrom <= ValidTo guaranteed)
            from fromOffset in Gen.Choose(-365, 0)
            from toOffset in Gen.Choose(0, 365)
            select new ValidityWindow(
                DateTime.UtcNow.AddDays(fromOffset),
                DateTime.UtcNow.AddDays(toOffset))
        );
    }

    /// <summary>
    /// Generates a validity window that is currently active (spans the current time).
    /// </summary>
    public static Gen<ValidityWindow> GenActiveValidityWindow()
    {
        return Gen.OneOf(
            // Both null (always valid)
            Gen.Constant(new ValidityWindow(null, null)),
            // Only ValidFrom in past
            from days in Gen.Choose(1, 365)
            select new ValidityWindow(DateTime.UtcNow.AddDays(-days), null),
            // Only ValidTo in future
            from days in Gen.Choose(1, 365)
            select new ValidityWindow(null, DateTime.UtcNow.AddDays(days)),
            // Both set, spanning current time
            from fromDaysAgo in Gen.Choose(1, 365)
            from toDaysAhead in Gen.Choose(1, 365)
            select new ValidityWindow(
                DateTime.UtcNow.AddDays(-fromDaysAgo),
                DateTime.UtcNow.AddDays(toDaysAhead))
        );
    }

    /// <summary>
    /// Generates a validity window that has expired (ValidTo is in the past).
    /// </summary>
    public static Gen<ValidityWindow> GenExpiredValidityWindow()
    {
        return from fromDaysAgo in Gen.Choose(2, 365)
               from toDaysAgo in Gen.Choose(1, fromDaysAgo - 1)
               select new ValidityWindow(
                   DateTime.UtcNow.AddDays(-fromDaysAgo),
                   DateTime.UtcNow.AddDays(-toDaysAgo));
    }

    /// <summary>
    /// Generates a complete permission chain: Tenant → Application → FunctionalArea → 
    /// PermissionType → Permission → User → Assignment. 
    /// Returns all generated entities as a PermissionScenario record.
    /// </summary>
    public static Gen<PermissionScenario> GenPermissionScenario()
    {
        var tenantId = Guid.NewGuid();
        var applicationId = Guid.NewGuid();

        return from tenant in GenTenant()
               from app in GenApplication(tenantId)
               from user in GenUserProfile(tenantId)
               from fa in GenFunctionalArea(tenantId, applicationId)
               from pt in GenPermissionType(tenantId, applicationId)
               from perm in GenPermission(tenantId, applicationId, fa.Id, pt.Id)
               select new PermissionScenario(
                   TenantId: tenantId,
                   ApplicationId: applicationId,
                   Tenant: tenant,
                   Application: app,
                   UserProfile: user,
                   FunctionalArea: fa,
                   PermissionType: pt,
                   Permission: perm);
    }
}

/// <summary>
/// Represents a temporal validity window for assignments.
/// </summary>
public record ValidityWindow(DateTime? ValidFrom, DateTime? ValidTo);

/// <summary>
/// A complete permission scenario with all related entities for property testing.
/// </summary>
public record PermissionScenario(
    Guid TenantId,
    Guid ApplicationId,
    Tenant Tenant,
    ApplicationEntity Application,
    UserProfile UserProfile,
    FunctionalArea FunctionalArea,
    PermissionType PermissionType,
    Permission Permission);
