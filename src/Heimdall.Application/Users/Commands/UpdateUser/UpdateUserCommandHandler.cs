using Heimdall.Domain.Entities;
using Heimdall.Domain.Enums;
using Heimdall.Domain.Interfaces;
using MediatR;

namespace Heimdall.Application.Users.Commands.UpdateUser;

public sealed class UpdateUserCommandHandler : IRequestHandler<UpdateUserCommand, UpdateUserResult>
{
    private readonly IRepository<UserProfile> _userRepository;

    public UpdateUserCommandHandler(IRepository<UserProfile> userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<UpdateUserResult> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
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
                $"Cannot update an inactive user profile.");
        }

        // Only update mutable fields
        user.Email = request.Email;
        user.DisplayName = request.DisplayName;

        await _userRepository.UpdateAsync(user, cancellationToken);

        return new UpdateUserResult
        {
            UserProfileId = user.Id,
            Success = true
        };
    }
}
