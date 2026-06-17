using Heimdall.Domain.Entities;
using Heimdall.Domain.Interfaces;
using MediatR;

namespace Heimdall.Application.FunctionalAreas.Commands.DeactivateFunctionalArea;

public sealed class DeactivateFunctionalAreaCommandHandler : IRequestHandler<DeactivateFunctionalAreaCommand, Unit>
{
    private readonly IRepository<FunctionalArea> _repository;

    public DeactivateFunctionalAreaCommandHandler(IRepository<FunctionalArea> repository)
    {
        _repository = repository;
    }

    public async Task<Unit> Handle(DeactivateFunctionalAreaCommand request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken);

        if (entity is null)
        {
            throw new InvalidOperationException($"FunctionalArea '{request.Id}' was not found.");
        }

        // Populate cache invalidation context
        request.TenantId = entity.TenantId;
        request.ApplicationId = entity.ApplicationId;

        entity.IsActive = false;

        await _repository.UpdateAsync(entity, cancellationToken);

        return Unit.Value;
    }
}
