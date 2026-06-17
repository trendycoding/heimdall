using Heimdall.Domain.Entities;
using Heimdall.Domain.Interfaces;
using MediatR;

namespace Heimdall.Application.PermissionTemplates.Commands.UpdatePermissionTemplate;

public sealed class UpdatePermissionTemplateCommandHandler : IRequestHandler<UpdatePermissionTemplateCommand, Unit>
{
    private readonly IRepository<PermissionTemplate> _repository;

    public UpdatePermissionTemplateCommandHandler(IRepository<PermissionTemplate> repository)
    {
        _repository = repository;
    }

    public async Task<Unit> Handle(UpdatePermissionTemplateCommand request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken);

        if (entity is null)
        {
            throw new InvalidOperationException($"PermissionTemplate '{request.Id}' was not found.");
        }

        entity.Name = request.Name;
        entity.Description = request.Description;
        entity.IsActive = request.IsActive;

        await _repository.UpdateAsync(entity, cancellationToken);

        return Unit.Value;
    }
}
