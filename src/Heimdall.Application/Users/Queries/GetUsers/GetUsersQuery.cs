using Heimdall.Application.Users.Queries.GetUser;
using MediatR;

namespace Heimdall.Application.Users.Queries.GetUsers;

/// <summary>
/// Retrieves all UserProfiles within the current tenant.
/// Supports optional filtering by identity provider or status.
/// </summary>
public sealed class GetUsersQuery : IRequest<IReadOnlyList<UserDto>>
{
    /// <summary>
    /// Optional filter by identity provider name.
    /// </summary>
    public string? IdentityProvider { get; init; }

    /// <summary>
    /// Optional filter by user status (Active, Inactive).
    /// </summary>
    public string? Status { get; init; }

    /// <summary>
    /// Optional filter by external subject ID (exact match).
    /// </summary>
    public string? ExternalSubjectId { get; init; }
}
