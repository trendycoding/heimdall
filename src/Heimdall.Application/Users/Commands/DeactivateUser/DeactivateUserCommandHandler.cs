using Heimdall.Domain.Entities;
using Heimdall.Domain.Enums;
using Heimdall.Domain.Interfaces;
using MediatR;

namespace Heimdall.Application.Users.Commands.DeactivateUser;

public sealed class DeactivateUserCommandHandler : IRequestHandler<DeactivateUserCommand, DeactivateUserResult>
{
    private readonly IRepository<UserProfile> _userRepository;

    public DeactivateUserCommandHandler(IRepository<UserProfile> userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<DeactivateUserResult> Handle(DeactivateUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserProfileId, cancellationToken);

        if (user is null)
        {
            throw new InvalidOperationException(
                $"User profile '{request.UserProfileId}' was not found.");
        }

        if (user.Status == UserStatus.Inactive)
        {
            throw new InvalidOperationException(
                $"User profile is already inactive.");
        }

        user.Status = UserStatus.Inactive;
        await _userRepository.UpdateAsync(user, cancellationToken);

        return new DeactivateUserResult
        {
            UserProfileId = user.Id,
            Success = true
        };
    }
}
