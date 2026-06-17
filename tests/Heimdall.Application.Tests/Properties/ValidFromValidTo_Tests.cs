using FsCheck;
using FsCheck.Xunit;
using Heimdall.Application.PermissionAssignments.Commands.CreateUserPermissionAssignment;
using Heimdall.Application.PermissionAssignments.Commands.CreateGroupPermissionAssignment;
using Heimdall.Domain.Enums;

namespace Heimdall.Application.Tests.Properties;

/// <summary>
/// Property 12: ValidFrom Must Not Exceed ValidTo
/// Generate permission assignments with ValidFrom > ValidTo; assert rejection.
///
/// **Validates: Requirements 9.7**
/// </summary>
public class ValidFromValidTo_Tests
{
    private readonly CreateUserPermissionAssignmentCommandValidator _userAssignmentValidator = new();
    private readonly CreateGroupPermissionAssignmentCommandValidator _groupAssignmentValidator = new();

    [Property(MaxTest = 200)]
    public bool UserPermissionAssignment_ValidFromAfterValidTo_IsRejected(
        PositiveInt offsetDaysRaw, PositiveInt baseOffsetRaw)
    {
        // Generate a ValidFrom that is strictly after ValidTo
        var baseDate = DateTime.UtcNow;
        var validTo = baseDate.AddDays(baseOffsetRaw.Get % 365);
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

        var result = _userAssignmentValidator.Validate(command);

        return !result.IsValid &&
               result.Errors.Any(e => e.ErrorMessage.Contains("ValidFrom"));
    }

    [Property(MaxTest = 200)]
    public bool GroupPermissionAssignment_ValidFromAfterValidTo_IsRejected(
        PositiveInt offsetDaysRaw, PositiveInt baseOffsetRaw)
    {
        // Generate a ValidFrom that is strictly after ValidTo
        var baseDate = DateTime.UtcNow;
        var validTo = baseDate.AddDays(baseOffsetRaw.Get % 365);
        var offsetDays = (offsetDaysRaw.Get % 1000) + 1; // At least 1 day after ValidTo
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

    [Property(MaxTest = 200)]
    public bool UserPermissionAssignment_ValidFromEqualsValidTo_IsAccepted(PositiveInt daysRaw)
    {
        // When ValidFrom == ValidTo, it should be accepted (valid single-instant window)
        var date = DateTime.UtcNow.AddDays(daysRaw.Get % 365);

        var command = new CreateUserPermissionAssignmentCommand
        {
            TenantId = Guid.NewGuid(),
            ApplicationId = Guid.NewGuid(),
            UserProfileId = Guid.NewGuid(),
            PermissionId = Guid.NewGuid(),
            Effect = Effect.Allow,
            ValidFrom = date,
            ValidTo = date
        };

        var result = _userAssignmentValidator.Validate(command);

        // Should not have a ValidFrom/ValidTo validation failure
        return !result.Errors.Any(e => e.ErrorMessage.Contains("ValidFrom"));
    }

    [Property(MaxTest = 200)]
    public bool UserPermissionAssignment_ValidFromBeforeValidTo_IsAccepted(
        PositiveInt baseDaysRaw, PositiveInt offsetDaysRaw)
    {
        // When ValidFrom < ValidTo, the date range rule should pass
        var baseDate = DateTime.UtcNow;
        var validFrom = baseDate.AddDays(baseDaysRaw.Get % 365);
        var validTo = validFrom.AddDays((offsetDaysRaw.Get % 365) + 1); // At least 1 day after

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

        var result = _userAssignmentValidator.Validate(command);

        // Should not have a ValidFrom/ValidTo validation failure
        return !result.Errors.Any(e => e.ErrorMessage.Contains("ValidFrom"));
    }

    [Property(MaxTest = 200)]
    public bool GroupPermissionAssignment_ValidFromEqualsValidTo_IsAccepted(PositiveInt daysRaw)
    {
        var date = DateTime.UtcNow.AddDays(daysRaw.Get % 365);

        var command = new CreateGroupPermissionAssignmentCommand
        {
            TenantId = Guid.NewGuid(),
            ApplicationId = Guid.NewGuid(),
            GroupId = Guid.NewGuid(),
            PermissionId = Guid.NewGuid(),
            Effect = Effect.Deny,
            ValidFrom = date,
            ValidTo = date
        };

        var result = _groupAssignmentValidator.Validate(command);

        return !result.Errors.Any(e => e.ErrorMessage.Contains("ValidFrom"));
    }

    [Property(MaxTest = 200)]
    public bool UserPermissionAssignment_NullDates_IsAccepted()
    {
        // Null ValidFrom and ValidTo should be accepted (unbounded window)
        var command = new CreateUserPermissionAssignmentCommand
        {
            TenantId = Guid.NewGuid(),
            ApplicationId = Guid.NewGuid(),
            UserProfileId = Guid.NewGuid(),
            PermissionId = Guid.NewGuid(),
            Effect = Effect.Allow,
            ValidFrom = null,
            ValidTo = null
        };

        var result = _userAssignmentValidator.Validate(command);

        return !result.Errors.Any(e => e.ErrorMessage.Contains("ValidFrom"));
    }

    [Property(MaxTest = 200)]
    public bool UserPermissionAssignment_OnlyValidFromSet_IsAccepted(PositiveInt daysRaw)
    {
        // Only ValidFrom set (ValidTo null = unbounded end)
        var validFrom = DateTime.UtcNow.AddDays(daysRaw.Get % 365);

        var command = new CreateUserPermissionAssignmentCommand
        {
            TenantId = Guid.NewGuid(),
            ApplicationId = Guid.NewGuid(),
            UserProfileId = Guid.NewGuid(),
            PermissionId = Guid.NewGuid(),
            Effect = Effect.Allow,
            ValidFrom = validFrom,
            ValidTo = null
        };

        var result = _userAssignmentValidator.Validate(command);

        return !result.Errors.Any(e => e.ErrorMessage.Contains("ValidFrom"));
    }

    [Property(MaxTest = 200)]
    public bool UserPermissionAssignment_OnlyValidToSet_IsAccepted(PositiveInt daysRaw)
    {
        // Only ValidTo set (ValidFrom null = unbounded start)
        var validTo = DateTime.UtcNow.AddDays(daysRaw.Get % 365);

        var command = new CreateUserPermissionAssignmentCommand
        {
            TenantId = Guid.NewGuid(),
            ApplicationId = Guid.NewGuid(),
            UserProfileId = Guid.NewGuid(),
            PermissionId = Guid.NewGuid(),
            Effect = Effect.Allow,
            ValidFrom = null,
            ValidTo = validTo
        };

        var result = _userAssignmentValidator.Validate(command);

        return !result.Errors.Any(e => e.ErrorMessage.Contains("ValidFrom"));
    }
}
