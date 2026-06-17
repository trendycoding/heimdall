using Heimdall.Application.Users.Queries.GetUser;
using Heimdall.Domain.Entities;
using Heimdall.Domain.Enums;
using Heimdall.Domain.Interfaces;
using MediatR;

namespace Heimdall.Application.Users.Queries.GetUsers;

public sealed class GetUsersQueryHandler : IRequestHandler<GetUsersQuery, IReadOnlyList<UserDto>>
{
    private readonly IRepository<UserProfile> _userRepository;

    public GetUsersQueryHandler(IRepository<UserProfile> userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<IReadOnlyList<UserDto>> Handle(GetUsersQuery request, CancellationToken cancellationToken)
    {
        var users = await _userRepository.GetAllAsync(cancellationToken);

        IEnumerable<UserProfile> filtered = users;

        if (!string.IsNullOrWhiteSpace(request.IdentityProvider))
        {
            filtered = filtered.Where(u =>
                u.IdentityProvider.Equals(request.IdentityProvider, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(request.Status) &&
            Enum.TryParse<UserStatus>(request.Status, ignoreCase: true, out var status))
        {
            filtered = filtered.Where(u => u.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(request.ExternalSubjectId))
        {
            filtered = filtered.Where(u => u.ExternalSubjectId == request.ExternalSubjectId);
        }

        return filtered.Select(MapToDto).ToList();
    }

    private static UserDto MapToDto(UserProfile user) => new()
    {
        UserProfileId = user.Id,
        TenantId = user.TenantId,
        ExternalSubjectId = user.ExternalSubjectId,
        IdentityProvider = user.IdentityProvider,
        Email = user.Email,
        DisplayName = user.DisplayName,
        Status = user.Status.ToString(),
        LastLoginAt = user.LastLoginAt,
        CreatedAt = user.CreatedAt,
        CreatedBy = user.CreatedBy,
        ModifiedAt = user.ModifiedAt,
        ModifiedBy = user.ModifiedBy
    };
}
