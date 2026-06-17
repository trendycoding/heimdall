using Heimdall.Domain.Entities;
using Heimdall.Domain.Interfaces;
using MediatR;

namespace Heimdall.Application.PermissionTemplates.Commands.DeactivatePermissionTemplate;

public sealed class DeactivatePermissionTemplateCommandHandler : IRequestHandler<DeactivatePermissionTemplateCommand, Unit>
{
    private readonly IRepository<PermissionTemplate> _repository;

    public DeactivatePermissionTemplateCommandHandler(IRepository<PermissionTemplate> repository)
    {
        _repository = repository;
    }

    public async Task<Unit> Handle(DeactivatePermissionTemplateCommand request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken);

        if (entity is null)
        {
            throw new InvalidOperationException($"PermissionTemplate '{request.Id}' was not found.");
        }

        entity.IsActive = false;

        await _repository.UpdateAsync(entity, cancellationToken);

        return Unit.Value;
    }
}
