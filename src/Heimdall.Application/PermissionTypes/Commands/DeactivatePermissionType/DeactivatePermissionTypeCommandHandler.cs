using Heimdall.Domain.Entities;
using Heimdall.Domain.Interfaces;
using MediatR;

namespace Heimdall.Application.PermissionTypes.Commands.DeactivatePermissionType;

public sealed class DeactivatePermissionTypeCommandHandler : IRequestHandler<DeactivatePermissionTypeCommand, Unit>
{
    private readonly IRepository<PermissionType> _repository;

    public DeactivatePermissionTypeCommandHandler(IRepository<PermissionType> repository)
    {
        _repository = repository;
    }

    public async Task<Unit> Handle(DeactivatePermissionTypeCommand request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken);

        if (entity is null)
        {
            throw new InvalidOperationException($"PermissionType '{request.Id}' was not found.");
        }

        // IsSystemReserved permission types cannot be deleted/deactivated
        if (entity.IsSystemReserved)
        {
            throw new InvalidOperationException("System-reserved permission types cannot be deactivated.");
        }

        // Populate cache invalidation context
        request.TenantId = entity.TenantId;
        request.ApplicationId = entity.ApplicationId;

        entity.IsActive = false;

        await _repository.UpdateAsync(entity, cancellationToken);

        return Unit.Value;
    }
}
