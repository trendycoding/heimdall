using Heimdall.Domain.Entities;
using Heimdall.Domain.Interfaces;
using MediatR;

namespace Heimdall.Application.AccessDetails.Commands.DeactivateGroupAccessDetail;

public sealed class DeactivateGroupAccessDetailCommandHandler : IRequestHandler<DeactivateGroupAccessDetailCommand, Unit>
{
    private readonly IRepository<GroupAccessDetail> _repository;

    public DeactivateGroupAccessDetailCommandHandler(IRepository<GroupAccessDetail> repository)
    {
        _repository = repository;
    }

    public async Task<Unit> Handle(DeactivateGroupAccessDetailCommand request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken);

        if (entity is null)
        {
            throw new InvalidOperationException($"GroupAccessDetail '{request.Id}' was not found.");
        }

        // Populate cache invalidation context
        request.TenantId = entity.TenantId;
        request.ApplicationId = entity.ApplicationId;

        entity.IsActive = false;

        await _repository.UpdateAsync(entity, cancellationToken);

        return Unit.Value;
    }
}
