using Heimdall.Domain.Entities;
using Heimdall.Domain.Interfaces;
using MediatR;

namespace Heimdall.Application.Permissions.Commands.DeactivatePermission;

public sealed class DeactivatePermissionCommandHandler : IRequestHandler<DeactivatePermissionCommand, Unit>
{
    private readonly IRepository<Permission> _repository;

    public DeactivatePermissionCommandHandler(IRepository<Permission> repository)
    {
        _repository = repository;
    }

    public async Task<Unit> Handle(DeactivatePermissionCommand request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken);

        if (entity is null)
        {
            throw new InvalidOperationException($"Permission '{request.Id}' was not found.");
        }

        // Populate cache invalidation context
        request.TenantId = entity.TenantId;
        request.ApplicationId = entity.ApplicationId;

        entity.IsActive = false;

        await _repository.UpdateAsync(entity, cancellationToken);

        return Unit.Value;
    }
}
