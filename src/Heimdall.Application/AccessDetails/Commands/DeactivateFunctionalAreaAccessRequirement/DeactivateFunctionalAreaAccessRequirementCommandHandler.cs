using Heimdall.Domain.Entities;
using Heimdall.Domain.Interfaces;
using MediatR;

namespace Heimdall.Application.AccessDetails.Commands.DeactivateFunctionalAreaAccessRequirement;

public sealed class DeactivateFunctionalAreaAccessRequirementCommandHandler
    : IRequestHandler<DeactivateFunctionalAreaAccessRequirementCommand, Unit>
{
    private readonly IRepository<FunctionalAreaAccessRequirement> _repository;

    public DeactivateFunctionalAreaAccessRequirementCommandHandler(IRepository<FunctionalAreaAccessRequirement> repository)
    {
        _repository = repository;
    }

    public async Task<Unit> Handle(DeactivateFunctionalAreaAccessRequirementCommand request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken);

        if (entity is null)
        {
            throw new InvalidOperationException($"FunctionalAreaAccessRequirement '{request.Id}' was not found.");
        }

        // Populate cache invalidation context
        request.TenantId = entity.TenantId;
        request.ApplicationId = entity.ApplicationId;

        entity.IsActive = false;

        await _repository.UpdateAsync(entity, cancellationToken);

        return Unit.Value;
    }
}
