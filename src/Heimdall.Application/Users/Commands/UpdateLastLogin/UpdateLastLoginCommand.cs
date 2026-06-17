using MediatR;

namespace Heimdall.Application.Users.Commands.UpdateLastLogin;

/// <summary>
/// Updates the LastLoginAt timestamp on a UserProfile when the user authenticates successfully.
/// This is a lightweight command triggered during the auth flow.
/// </summary>
public sealed class UpdateLastLoginCommand : IRequest<Unit>
{
    public Guid TenantId { get; init; }
    public Guid UserProfileId { get; init; }
    public DateTime LoginTimestamp { get; init; } = DateTime.UtcNow;
}
