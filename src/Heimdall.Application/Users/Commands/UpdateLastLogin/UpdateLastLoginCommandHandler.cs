using Heimdall.Domain.Entities;
using Heimdall.Domain.Interfaces;
using MediatR;

namespace Heimdall.Application.Users.Commands.UpdateLastLogin;

public sealed class UpdateLastLoginCommandHandler : IRequestHandler<UpdateLastLoginCommand, Unit>
{
    private readonly IRepository<UserProfile> _userRepository;

    public UpdateLastLoginCommandHandler(IRepository<UserProfile> userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<Unit> Handle(UpdateLastLoginCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserProfileId, cancellationToken);

        if (user is null)
        {
            // Silently ignore if user not found during auth flow — don't block authentication
            return Unit.Value;
        }

        user.LastLoginAt = request.LoginTimestamp;
        await _userRepository.UpdateAsync(user, cancellationToken);

        return Unit.Value;
    }
}
