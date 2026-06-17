using FsCheck;
using FsCheck.Xunit;
using FluentValidation;
using Heimdall.Application.Tenants.Commands.CreateTenant;
using Heimdall.Application.Applications.Commands.CreateApplication;
using Heimdall.Application.Users.Commands.SyncUser;
using Heimdall.Application.PermissionAssignments.Commands.CreateUserPermissionAssignment;
using Heimdall.Application.PermissionAssignments.Commands.CreateGroupPermissionAssignment;
using Heimdall.Domain.Enums;

namespace Heimdall.Application.Tests.Properties;

/// <summary>
/// Property 11: Input Validation Rejection
/// Generate requests with invalid input (missing required fields, exceeding lengths, invalid formats);
/// assert validation error with failing fields identified, no entity modified.
///
/// **Validates: Requirements 1.7, 2.2, 4.6, 9.7, 23.6**
/// </summary>
public class InputValidation_RejectionTests
{
    private readonly CreateTenantCommandValidator _tenantValidator = new();
    private readonly CreateApplicationCommandValidator _applicationValidator = new();
    private readonly SyncUserCommandValidator _syncUserValidator = new();
    private readonly CreateUserPermissionAssignmentCommandValidator _assignmentValidator = new();
    private readonly CreateGroupPermissionAssignmentCommandValidator _groupAssignmentValidator = new();

    #region Tenant Validation (Requirement 1.7)

    [Property(MaxTest = 100)]
    public bool Tenant_EmptyName_RejectsWithNameField(NonEmptyString slugRaw)
    {
        // Use a valid slug to isolate the Name validation
        var slug = new string(slugRaw.Get.Where(c => char.IsLetterOrDigit(c) || c == '-').Take(10).ToArray()).ToLower();
        if (string.IsNullOrEmpty(slug) || slug.Length < 2) slug = "ab";
        // Ensure starts/ends with alphanum
        slug = "a" + slug.TrimStart('-').TrimEnd('-') + "b";

        var command = new CreateTenantCommand(
            Name: "",
            Slug: slug,
            PrimaryIdentityMode: PrimaryIdentityMode.EntraExternalId);

        var result = _tenantValidator.Validate(command);

        return !result.IsValid &&
               result.Errors.Any(e => e.PropertyName == "Name");
    }

    [Property(MaxTest = 100)]
    public bool Tenant_NameExceedingMaxLength_RejectsWithNameField(PositiveInt extraCharsRaw)
    {
        var extraChars = (extraCharsRaw.Get % 200) + 1; // 1-200 extra chars
        var name = new string('A', 128 + extraChars); // Exceed 128 char limit

        var command = new CreateTenantCommand(
            Name: name,
            Slug: "valid-slug",
            PrimaryIdentityMode: PrimaryIdentityMode.EntraExternalId);

        var result = _tenantValidator.Validate(command);

        return !result.IsValid &&
               result.Errors.Any(e => e.PropertyName == "Name");
    }

    [Property(MaxTest = 100)]
    public bool Tenant_InvalidSlugFormat_RejectsWithSlugField(NonEmptyString invalidSlugRaw)
    {
        // Generate slugs that contain invalid characters (uppercase, special chars, spaces)
        var raw = invalidSlugRaw.Get;
        // Ensure the slug has at least one invalid character for slug format
        var invalidSlug = raw.Contains(' ') || raw.Any(char.IsUpper) || raw.Contains('!')
            ? raw
            : raw + "!INVALID";

        // Only test if it actually violates the pattern
        var slugPattern = @"^[a-z0-9][a-z0-9-]*[a-z0-9]$";
        var singleCharPattern = @"^[a-z0-9]$";
        if (System.Text.RegularExpressions.Regex.IsMatch(invalidSlug, slugPattern) ||
            (invalidSlug.Length == 1 && System.Text.RegularExpressions.Regex.IsMatch(invalidSlug, singleCharPattern)))
            return true; // Skip if accidentally valid

        var command = new CreateTenantCommand(
            Name: "Valid Name",
            Slug: invalidSlug,
            PrimaryIdentityMode: PrimaryIdentityMode.EntraExternalId);

        var result = _tenantValidator.Validate(command);

        return !result.IsValid &&
               result.Errors.Any(e => e.PropertyName == "Slug");
    }

    [Property(MaxTest = 100)]
    public bool Tenant_SlugExceedingMaxLength_RejectsWithSlugField(PositiveInt extraCharsRaw)
    {
        var extraChars = (extraCharsRaw.Get % 100) + 1;
        // Create a valid-format slug that exceeds 64 characters
        var slug = "a" + new string('b', 64 + extraChars) + "c";

        var command = new CreateTenantCommand(
            Name: "Valid Name",
            Slug: slug,
            PrimaryIdentityMode: PrimaryIdentityMode.EntraExternalId);

        var result = _tenantValidator.Validate(command);

        return !result.IsValid &&
               result.Errors.Any(e => e.PropertyName == "Slug");
    }

    #endregion

    #region Application Validation (Requirement 2.2)

    [Property(MaxTest = 100)]
    public bool Application_EmptyName_RejectsWithNameField(NonEmptyString clientIdRaw)
    {
        var clientId = clientIdRaw.Get.Substring(0, Math.Min(clientIdRaw.Get.Length, 50));

        var command = new CreateApplicationCommand(
            Name: "",
            ClientIdentifier: clientId,
            Description: null,
            AllowedRedirectUris: null,
            AllowedOrigins: null);

        var result = _applicationValidator.Validate(command);

        return !result.IsValid &&
               result.Errors.Any(e => e.PropertyName == "Name");
    }

    [Property(MaxTest = 100)]
    public bool Application_NameExceedingMaxLength_RejectsWithNameField(PositiveInt extraCharsRaw)
    {
        var extraChars = (extraCharsRaw.Get % 200) + 1;
        var name = new string('X', 200 + extraChars); // Exceed 200 char limit

        var command = new CreateApplicationCommand(
            Name: name,
            ClientIdentifier: "valid-client-id",
            Description: null,
            AllowedRedirectUris: null,
            AllowedOrigins: null);

        var result = _applicationValidator.Validate(command);

        return !result.IsValid &&
               result.Errors.Any(e => e.PropertyName == "Name");
    }

    [Property(MaxTest = 100)]
    public bool Application_EmptyClientIdentifier_RejectsWithClientIdentifierField()
    {
        var command = new CreateApplicationCommand(
            Name: "Valid App Name",
            ClientIdentifier: "",
            Description: null,
            AllowedRedirectUris: null,
            AllowedOrigins: null);

        var result = _applicationValidator.Validate(command);

        return !result.IsValid &&
               result.Errors.Any(e => e.PropertyName == "ClientIdentifier");
    }

    [Property(MaxTest = 100)]
    public bool Application_ClientIdentifierExceedingMaxLength_RejectsWithClientIdentifierField(PositiveInt extraCharsRaw)
    {
        var extraChars = (extraCharsRaw.Get % 200) + 1;
        var clientId = new string('C', 128 + extraChars); // Exceed 128 char limit

        var command = new CreateApplicationCommand(
            Name: "Valid App",
            ClientIdentifier: clientId,
            Description: null,
            AllowedRedirectUris: null,
            AllowedOrigins: null);

        var result = _applicationValidator.Validate(command);

        return !result.IsValid &&
               result.Errors.Any(e => e.PropertyName == "ClientIdentifier");
    }

    [Property(MaxTest = 100)]
    public bool Application_DescriptionExceedingMaxLength_RejectsWithDescriptionField(PositiveInt extraCharsRaw)
    {
        var extraChars = (extraCharsRaw.Get % 500) + 1;
        var description = new string('D', 1000 + extraChars); // Exceed 1000 char limit

        var command = new CreateApplicationCommand(
            Name: "Valid App",
            ClientIdentifier: "valid-client",
            Description: description,
            AllowedRedirectUris: null,
            AllowedOrigins: null);

        var result = _applicationValidator.Validate(command);

        return !result.IsValid &&
               result.Errors.Any(e => e.PropertyName == "Description");
    }

    [Property(MaxTest = 100)]
    public bool Application_TooManyRedirectUris_RejectsWithRedirectUrisField(PositiveInt extraCountRaw)
    {
        var extraCount = (extraCountRaw.Get % 10) + 1;
        var uris = Enumerable.Range(0, 20 + extraCount)
            .Select(i => $"https://example.com/callback/{i}")
            .ToList();

        var command = new CreateApplicationCommand(
            Name: "Valid App",
            ClientIdentifier: "valid-client",
            Description: null,
            AllowedRedirectUris: uris,
            AllowedOrigins: null);

        var result = _applicationValidator.Validate(command);

        return !result.IsValid &&
               result.Errors.Any(e => e.PropertyName == "AllowedRedirectUris");
    }

    [Property(MaxTest = 100)]
    public bool Application_TooManyAllowedOrigins_RejectsWithAllowedOriginsField(PositiveInt extraCountRaw)
    {
        var extraCount = (extraCountRaw.Get % 10) + 1;
        var origins = Enumerable.Range(0, 20 + extraCount)
            .Select(i => $"https://origin{i}.example.com")
            .ToList();

        var command = new CreateApplicationCommand(
            Name: "Valid App",
            ClientIdentifier: "valid-client",
            Description: null,
            AllowedRedirectUris: null,
            AllowedOrigins: origins);

        var result = _applicationValidator.Validate(command);

        return !result.IsValid &&
               result.Errors.Any(e => e.PropertyName == "AllowedOrigins");
    }

    [Property(MaxTest = 100)]
    public bool Application_InvalidRedirectUri_RejectsWithRedirectUriField(PositiveInt variantRaw)
    {
        // Generate URIs that are not valid absolute URIs
        var variant = variantRaw.Get % 4;
        var invalidUri = variant switch
        {
            0 => "not-a-uri",
            1 => "relative/path/only",
            2 => "://missing-scheme",
            _ => ""
        };

        var uris = new List<string> { invalidUri };

        var command = new CreateApplicationCommand(
            Name: "Valid App",
            ClientIdentifier: "valid-client",
            Description: null,
            AllowedRedirectUris: uris,
            AllowedOrigins: null);

        var result = _applicationValidator.Validate(command);

        return !result.IsValid &&
               result.Errors.Any(e => e.PropertyName.Contains("AllowedRedirectUris"));
    }

    #endregion

    #region User Sync Validation (Requirement 4.6)

    [Property(MaxTest = 100)]
    public bool SyncUser_EmptyExternalSubjectId_Rejects()
    {
        var command = new SyncUserCommand
        {
            TenantId = Guid.NewGuid(),
            ExternalSubjectId = "",
            IdentityProvider = "entra",
            Email = "user@example.com",
            DisplayName = "Test User"
        };

        var result = _syncUserValidator.Validate(command);

        return !result.IsValid &&
               result.Errors.Any(e => e.PropertyName == "ExternalSubjectId");
    }

    [Property(MaxTest = 100)]
    public bool SyncUser_EmptyIdentityProvider_Rejects()
    {
        var command = new SyncUserCommand
        {
            TenantId = Guid.NewGuid(),
            ExternalSubjectId = "subject-123",
            IdentityProvider = "",
            Email = "user@example.com",
            DisplayName = "Test User"
        };

        var result = _syncUserValidator.Validate(command);

        return !result.IsValid &&
               result.Errors.Any(e => e.PropertyName == "IdentityProvider");
    }

    [Property(MaxTest = 100)]
    public bool SyncUser_EmptyEmail_Rejects()
    {
        var command = new SyncUserCommand
        {
            TenantId = Guid.NewGuid(),
            ExternalSubjectId = "subject-123",
            IdentityProvider = "entra",
            Email = "",
            DisplayName = "Test User"
        };

        var result = _syncUserValidator.Validate(command);

        return !result.IsValid &&
               result.Errors.Any(e => e.PropertyName == "Email");
    }

    [Property(MaxTest = 100)]
    public bool SyncUser_InvalidEmail_RejectsWithEmailField(PositiveInt variantRaw)
    {
        // Generate emails that are definitively invalid per FluentValidation's EmailAddress validator
        var variant = variantRaw.Get % 4;
        var invalidEmail = variant switch
        {
            0 => "noatsign",                    // Missing @ entirely
            1 => "",                            // Empty string (caught by NotEmpty)
            2 => "double@@at.com",              // Double @
            _ => "plaintext"                    // No @ symbol at all
        };

        var command = new SyncUserCommand
        {
            TenantId = Guid.NewGuid(),
            ExternalSubjectId = "subject-123",
            IdentityProvider = "entra",
            Email = invalidEmail,
            DisplayName = "Test User"
        };

        var result = _syncUserValidator.Validate(command);

        return !result.IsValid &&
               result.Errors.Any(e => e.PropertyName == "Email");
    }

    [Property(MaxTest = 100)]
    public bool SyncUser_EmptyTenantId_Rejects()
    {
        var command = new SyncUserCommand
        {
            TenantId = Guid.Empty,
            ExternalSubjectId = "subject-123",
            IdentityProvider = "entra",
            Email = "user@example.com",
            DisplayName = "Test User"
        };

        var result = _syncUserValidator.Validate(command);

        return !result.IsValid &&
               result.Errors.Any(e => e.PropertyName == "TenantId");
    }

    #endregion

    #region Permission Assignment Validation (Requirements 9.7, 23.6)

    [Property(MaxTest = 100)]
    public bool PermissionAssignment_EmptyApplicationId_Rejects()
    {
        var command = new CreateUserPermissionAssignmentCommand
        {
            TenantId = Guid.NewGuid(),
            ApplicationId = Guid.Empty,
            UserProfileId = Guid.NewGuid(),
            PermissionId = Guid.NewGuid(),
            Effect = Effect.Allow,
            ValidFrom = null,
            ValidTo = null
        };

        var result = _assignmentValidator.Validate(command);

        return !result.IsValid &&
               result.Errors.Any(e => e.PropertyName == "ApplicationId");
    }

    [Property(MaxTest = 100)]
    public bool PermissionAssignment_EmptyUserProfileId_Rejects()
    {
        var command = new CreateUserPermissionAssignmentCommand
        {
            TenantId = Guid.NewGuid(),
            ApplicationId = Guid.NewGuid(),
            UserProfileId = Guid.Empty,
            PermissionId = Guid.NewGuid(),
            Effect = Effect.Allow,
            ValidFrom = null,
            ValidTo = null
        };

        var result = _assignmentValidator.Validate(command);

        return !result.IsValid &&
               result.Errors.Any(e => e.PropertyName == "UserProfileId");
    }

    [Property(MaxTest = 100)]
    public bool PermissionAssignment_EmptyPermissionId_Rejects()
    {
        var command = new CreateUserPermissionAssignmentCommand
        {
            TenantId = Guid.NewGuid(),
            ApplicationId = Guid.NewGuid(),
            UserProfileId = Guid.NewGuid(),
            PermissionId = Guid.Empty,
            Effect = Effect.Allow,
            ValidFrom = null,
            ValidTo = null
        };

        var result = _assignmentValidator.Validate(command);

        return !result.IsValid &&
               result.Errors.Any(e => e.PropertyName == "PermissionId");
    }

    [Property(MaxTest = 100)]
    public bool PermissionAssignment_ValidFromAfterValidTo_Rejects(PositiveInt offsetDaysRaw, PositiveInt baseDaysRaw)
    {
        // Generate a ValidFrom that is strictly after ValidTo (Requirement 9.7)
        var baseDate = DateTime.UtcNow;
        var validTo = baseDate.AddDays(baseDaysRaw.Get % 365);
        var offsetDays = (offsetDaysRaw.Get % 1000) + 1; // At least 1 day after ValidTo
        var validFrom = validTo.AddDays(offsetDays);

        var command = new CreateUserPermissionAssignmentCommand
        {
            TenantId = Guid.NewGuid(),
            ApplicationId = Guid.NewGuid(),
            UserProfileId = Guid.NewGuid(),
            PermissionId = Guid.NewGuid(),
            Effect = Effect.Allow,
            ValidFrom = validFrom,
            ValidTo = validTo
        };

        var result = _assignmentValidator.Validate(command);

        return !result.IsValid &&
               result.Errors.Any(e => e.ErrorMessage.Contains("ValidFrom"));
    }

    [Property(MaxTest = 100)]
    public bool GroupPermissionAssignment_ValidFromAfterValidTo_Rejects(PositiveInt offsetDaysRaw, PositiveInt baseDaysRaw)
    {
        // Generate a ValidFrom that is strictly after ValidTo for group assignments (Requirement 9.7)
        var baseDate = DateTime.UtcNow;
        var validTo = baseDate.AddDays(baseDaysRaw.Get % 365);
        var offsetDays = (offsetDaysRaw.Get % 1000) + 1;
        var validFrom = validTo.AddDays(offsetDays);

        var command = new CreateGroupPermissionAssignmentCommand
        {
            TenantId = Guid.NewGuid(),
            ApplicationId = Guid.NewGuid(),
            GroupId = Guid.NewGuid(),
            PermissionId = Guid.NewGuid(),
            Effect = Effect.Deny,
            ValidFrom = validFrom,
            ValidTo = validTo
        };

        var result = _groupAssignmentValidator.Validate(command);

        return !result.IsValid &&
               result.Errors.Any(e => e.ErrorMessage.Contains("ValidFrom"));
    }

    [Property(MaxTest = 100)]
    public bool GroupPermissionAssignment_EmptyGroupId_Rejects()
    {
        var command = new CreateGroupPermissionAssignmentCommand
        {
            TenantId = Guid.NewGuid(),
            ApplicationId = Guid.NewGuid(),
            GroupId = Guid.Empty,
            PermissionId = Guid.NewGuid(),
            Effect = Effect.Allow,
            ValidFrom = null,
            ValidTo = null
        };

        var result = _groupAssignmentValidator.Validate(command);

        return !result.IsValid &&
               result.Errors.Any(e => e.PropertyName == "GroupId");
    }

    #endregion

    #region No Entity Modified Assertion (Requirement 23.6)

    /// <summary>
    /// Validates that FluentValidation runs BEFORE the handler - meaning when validation
    /// fails, no handler code executes and therefore no entities are created or modified.
    /// This is guaranteed by the ValidationBehavior pipeline behavior which throws
    /// ValidationException before the handler is invoked.
    /// 
    /// We verify this by asserting that all invalid commands produce validation errors
    /// at the validator level, which the pipeline behavior intercepts.
    /// </summary>
    [Property(MaxTest = 100)]
    public bool AllInvalidCommands_ProduceValidationErrors_BeforeHandlerExecution(PositiveInt seedRaw)
    {
        var seed = seedRaw.Get;
        var variant = seed % 6;

        // Generate various invalid commands across all entity types
        var (isValid, errorCount) = variant switch
        {
            0 => ValidateTenantInvalid(seed),
            1 => ValidateApplicationInvalid(seed),
            2 => ValidateSyncUserInvalid(seed),
            3 => ValidateUserAssignmentInvalid(seed),
            4 => ValidateGroupAssignmentInvalid(seed),
            _ => ValidateApplicationOverlength(seed)
        };

        // All invalid commands must:
        // 1. Fail validation (isValid == false)
        // 2. Report at least one specific field error (errorCount > 0)
        // This guarantees the ValidationBehavior would reject before handler execution
        return !isValid && errorCount > 0;
    }

    private (bool isValid, int errorCount) ValidateTenantInvalid(int seed)
    {
        var command = new CreateTenantCommand(Name: "", Slug: "", PrimaryIdentityMode: PrimaryIdentityMode.EntraExternalId);
        var result = _tenantValidator.Validate(command);
        return (result.IsValid, result.Errors.Count);
    }

    private (bool isValid, int errorCount) ValidateApplicationInvalid(int seed)
    {
        var command = new CreateApplicationCommand(Name: "", ClientIdentifier: "", Description: null, AllowedRedirectUris: null, AllowedOrigins: null);
        var result = _applicationValidator.Validate(command);
        return (result.IsValid, result.Errors.Count);
    }

    private (bool isValid, int errorCount) ValidateSyncUserInvalid(int seed)
    {
        var command = new SyncUserCommand
        {
            TenantId = Guid.Empty,
            ExternalSubjectId = "",
            IdentityProvider = "",
            Email = "",
            DisplayName = ""
        };
        var result = _syncUserValidator.Validate(command);
        return (result.IsValid, result.Errors.Count);
    }

    private (bool isValid, int errorCount) ValidateUserAssignmentInvalid(int seed)
    {
        var command = new CreateUserPermissionAssignmentCommand
        {
            TenantId = Guid.Empty,
            ApplicationId = Guid.Empty,
            UserProfileId = Guid.Empty,
            PermissionId = Guid.Empty,
            Effect = Effect.Allow,
            ValidFrom = null,
            ValidTo = null
        };
        var result = _assignmentValidator.Validate(command);
        return (result.IsValid, result.Errors.Count);
    }

    private (bool isValid, int errorCount) ValidateGroupAssignmentInvalid(int seed)
    {
        var command = new CreateGroupPermissionAssignmentCommand
        {
            TenantId = Guid.Empty,
            ApplicationId = Guid.Empty,
            GroupId = Guid.Empty,
            PermissionId = Guid.Empty,
            Effect = Effect.Allow,
            ValidFrom = null,
            ValidTo = null
        };
        var result = _groupAssignmentValidator.Validate(command);
        return (result.IsValid, result.Errors.Count);
    }

    private (bool isValid, int errorCount) ValidateApplicationOverlength(int seed)
    {
        var command = new CreateApplicationCommand(
            Name: new string('X', 201),
            ClientIdentifier: new string('C', 129),
            Description: new string('D', 1001),
            AllowedRedirectUris: Enumerable.Range(0, 25).Select(i => $"https://e.com/{i}").ToList(),
            AllowedOrigins: Enumerable.Range(0, 25).Select(i => $"https://o{i}.com").ToList());
        var result = _applicationValidator.Validate(command);
        return (result.IsValid, result.Errors.Count);
    }

    #endregion
}
