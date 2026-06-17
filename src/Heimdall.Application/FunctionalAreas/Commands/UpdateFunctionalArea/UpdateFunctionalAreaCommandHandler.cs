using Heimdall.Domain.Entities;
using Heimdall.Domain.Interfaces;
using MediatR;

namespace Heimdall.Application.FunctionalAreas.Commands.UpdateFunctionalArea;

public sealed class UpdateFunctionalAreaCommandHandler : IRequestHandler<UpdateFunctionalAreaCommand, Unit>
{
    private readonly IRepository<FunctionalArea> _repository;

    public UpdateFunctionalAreaCommandHandler(IRepository<FunctionalArea> repository)
    {
        _repository = repository;
    }

    public async Task<Unit> Handle(UpdateFunctionalAreaCommand request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken);

        if (entity is null)
        {
            throw new InvalidOperationException($"FunctionalArea '{request.Id}' was not found.");
        }

        entity.Name = request.Name;
        entity.Description = request.Description;

        await _repository.UpdateAsync(entity, cancellationToken);

        return Unit.Value;
    }
}
