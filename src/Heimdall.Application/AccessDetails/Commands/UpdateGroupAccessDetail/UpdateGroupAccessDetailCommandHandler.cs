using Heimdall.Domain.Entities;
using Heimdall.Domain.Interfaces;
using MediatR;

namespace Heimdall.Application.AccessDetails.Commands.UpdateGroupAccessDetail;

public sealed class UpdateGroupAccessDetailCommandHandler : IRequestHandler<UpdateGroupAccessDetailCommand, Unit>
{
    private readonly IRepository<GroupAccessDetail> _repository;

    public UpdateGroupAccessDetailCommandHandler(IRepository<GroupAccessDetail> repository)
    {
        _repository = repository;
    }

    public async Task<Unit> Handle(UpdateGroupAccessDetailCommand request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken);

        if (entity is null)
        {
            throw new InvalidOperationException($"GroupAccessDetail '{request.Id}' was not found.");
        }

        // Populate cache invalidation context
        request.TenantId = entity.TenantId;
        request.ApplicationId = entity.ApplicationId;

        entity.AccessDetailValue = request.AccessDetailValue;
        entity.Description = request.Description;
        entity.ValidFrom = request.ValidFrom;
        entity.ValidTo = request.ValidTo;

        await _repository.UpdateAsync(entity, cancellationToken);

        return Unit.Value;
    }
}
