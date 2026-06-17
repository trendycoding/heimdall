using MediatR;

namespace Heimdall.Application.Users.Queries.GetUser;

/// <summary>
/// Retrieves a single UserProfile by its Id within the current tenant.
/// </summary>
public sealed class GetUserQuery : IRequest<UserDto?>
{
    public Guid UserProfileId { get; init; }
}

public sealed class UserDto
{
    public Guid UserProfileId { get; init; }
    public Guid TenantId { get; init; }
    public string ExternalSubjectId { get; init; } = string.Empty;
    public string IdentityProvider { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime? LastLoginAt { get; init; }
    public DateTime CreatedAt { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
    public DateTime? ModifiedAt { get; init; }
    public string? ModifiedBy { get; init; }
}
