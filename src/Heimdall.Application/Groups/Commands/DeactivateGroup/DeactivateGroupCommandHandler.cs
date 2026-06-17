using Heimdall.Domain.Entities;
using Heimdall.Domain.Interfaces;
using MediatR;

namespace Heimdall.Application.Groups.Commands.DeactivateGroup;

public sealed class DeactivateGroupCommandHandler : IRequestHandler<DeactivateGroupCommand, Unit>
{
    private readonly IRepository<Group> _repository;

    public DeactivateGroupCommandHandler(IRepository<Group> repository)
    {
        _repository = repository;
    }

    public async Task<Unit> Handle(DeactivateGroupCommand request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken);

        if (entity is null)
        {
            throw new InvalidOperationException($"Group '{request.Id}' was not found.");
        }

        // Populate cache invalidation context
        request.TenantId = entity.TenantId;
        request.ApplicationId = entity.ApplicationId;

        entity.IsActive = false;

        await _repository.UpdateAsync(entity, cancellationToken);

        return Unit.Value;
    }
}
