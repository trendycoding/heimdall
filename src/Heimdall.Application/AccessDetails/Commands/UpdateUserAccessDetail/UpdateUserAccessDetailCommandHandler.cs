using Heimdall.Domain.Entities;
using Heimdall.Domain.Interfaces;
using MediatR;

namespace Heimdall.Application.AccessDetails.Commands.UpdateUserAccessDetail;

public sealed class UpdateUserAccessDetailCommandHandler : IRequestHandler<UpdateUserAccessDetailCommand, Unit>
{
    private readonly IRepository<UserAccessDetail> _repository;

    public UpdateUserAccessDetailCommandHandler(IRepository<UserAccessDetail> repository)
    {
        _repository = repository;
    }

    public async Task<Unit> Handle(UpdateUserAccessDetailCommand request, CancellationToken cancellationToken)
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

        entity.AccessDetailValue = request.AccessDetailValue;
        entity.Description = request.Description;
        entity.ValidFrom = request.ValidFrom;
        entity.ValidTo = request.ValidTo;

        await _repository.UpdateAsync(entity, cancellationToken);

        return Unit.Value;
    }
}
