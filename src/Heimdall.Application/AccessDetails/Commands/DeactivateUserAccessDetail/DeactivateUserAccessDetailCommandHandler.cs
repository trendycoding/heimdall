using Heimdall.Domain.Entities;
using Heimdall.Domain.Interfaces;
using MediatR;

namespace Heimdall.Application.AccessDetails.Commands.DeactivateUserAccessDetail;

public sealed class DeactivateUserAccessDetailCommandHandler : IRequestHandler<DeactivateUserAccessDetailCommand, Unit>
{
    private readonly IRepository<UserAccessDetail> _repository;

    public DeactivateUserAccessDetailCommandHandler(IRepository<UserAccessDetail> repository)
    {
        _repository = repository;
    }

    public async Task<Unit> Handle(DeactivateUserAccessDetailCommand request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken);

        if (entity is null)
        {
            throw new InvalidOperationException($"UserAccessDetail '{request.Id}' was not found.");
        }

        // Populate cache invalidation context
        request.TenantId = entity.TenantId;
        request.ApplicationId = entity.ApplicationId;
        request.UserProfileId = entity.UserProfileId;

        entity.IsActive = false;

        await _repository.UpdateAsync(entity, cancellationToken);

        return Unit.Value;
    }
}
