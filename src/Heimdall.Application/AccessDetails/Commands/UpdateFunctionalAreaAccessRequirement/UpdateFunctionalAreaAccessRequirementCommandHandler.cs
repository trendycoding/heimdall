using Heimdall.Domain.Entities;
using Heimdall.Domain.Interfaces;
using MediatR;

namespace Heimdall.Application.AccessDetails.Commands.UpdateFunctionalAreaAccessRequirement;

public sealed class UpdateFunctionalAreaAccessRequirementCommandHandler
    : IRequestHandler<UpdateFunctionalAreaAccessRequirementCommand, Unit>
{
    private readonly IRepository<FunctionalAreaAccessRequirement> _repository;

    public UpdateFunctionalAreaAccessRequirementCommandHandler(IRepository<FunctionalAreaAccessRequirement> repository)
    {
        _repository = repository;
    }

    public async Task<Unit> Handle(UpdateFunctionalAreaAccessRequirementCommand request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken);

        if (entity is null)
        {
            throw new InvalidOperationException($"FunctionalAreaAccessRequirement '{request.Id}' was not found.");
        }

        // Populate cache invalidation context
        request.TenantId = entity.TenantId;
        request.ApplicationId = entity.ApplicationId;

        entity.IsRequired = request.IsRequired;
        entity.Description = request.Description;

        await _repository.UpdateAsync(entity, cancellationToken);

        return Unit.Value;
    }
}
