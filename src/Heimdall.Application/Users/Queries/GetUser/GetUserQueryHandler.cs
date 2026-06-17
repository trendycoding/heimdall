using Heimdall.Domain.Entities;
using Heimdall.Domain.Interfaces;
using MediatR;

namespace Heimdall.Application.Users.Queries.GetUser;

public sealed class GetUserQueryHandler : IRequestHandler<GetUserQuery, UserDto?>
{
    private readonly IRepository<UserProfile> _userRepository;

    public GetUserQueryHandler(IRepository<UserProfile> userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<UserDto?> Handle(GetUserQuery request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserProfileId, cancellationToken);

        if (user is null)
            return null;

        return MapToDto(user);
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
