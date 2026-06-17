namespace Heimdall.Domain.ValueObjects;

/// <summary>
/// Maps a platform-recognized field to a source claim name from an identity provider token.
/// PlatformField values: Subject, Email, DisplayName, Groups, Roles, Tenant, Application
/// </summary>
public record ClaimMapping(string PlatformField, string SourceClaimName);
