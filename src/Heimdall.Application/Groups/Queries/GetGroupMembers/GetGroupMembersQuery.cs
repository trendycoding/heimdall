using MediatR;

namespace Heimdall.Application.Groups.Queries.GetGroupMembers;

public sealed record GetGroupMembersQuery(Guid GroupId) : IRequest<IReadOnlyList<GroupMemberDto>>;

public sealed record GroupMemberDto(
    Guid GroupMembershipId,
    Guid GroupId,
    Guid UserProfileId,
    Guid TenantId,
    Guid ApplicationId,
    DateTime CreatedAt,
    string CreatedBy);
