using Heimdall.Application.Common.Interfaces;
using MediatR;

namespace Heimdall.Application.Users.Commands.UpdateUser;

/// <summary>
/// Updates mutable fields (Email, DisplayName) on an existing UserProfile.
/// </summary>
public sealed class UpdateUserCommand : IRequest<UpdateUserResult>, IAuditableCommand
{
    public Guid TenantId { get; init; }
    public Guid UserProfileId { get; init; }
    public string Email { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;

    // IAuditableCommand
    public string EntityType => "UserProfile";
    public Guid EntityId => UserProfileId;
    public string AuditAction => "Update";
}

public sealed class UpdateUserResult
{
    public Guid UserProfileId { get; init; }
    public bool Success { get; init; }
}
