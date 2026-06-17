using Heimdall.Application.Common.Interfaces;
using Heimdall.Domain.Entities;
using Heimdall.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Heimdall.Application.PermissionTypes.Commands.UpdatePermissionType;

public sealed class UpdatePermissionTypeCommandHandler : IRequestHandler<UpdatePermissionTypeCommand, Unit>
{
    private readonly IRepository<PermissionType> _repository;
    private readonly IHeimdallDbContext _dbContext;

    public UpdatePermissionTypeCommandHandler(
        IRepository<PermissionType> repository,
        IHeimdallDbContext dbContext)
    {
        _repository = repository;
        _dbContext = dbContext;
    }

    public async Task<Unit> Handle(UpdatePermissionTypeCommand request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken);

        if (entity is null)
        {
            throw new InvalidOperationException($"PermissionType '{request.Id}' was not found.");
        }

        // Enforce case-insensitive uniqueness of Code within the application (excluding current entity)
        var codeExists = await _dbContext.PermissionTypes
            .AnyAsync(pt => pt.ApplicationId == entity.ApplicationId
                         && pt.Id != entity.Id
                         && pt.Code.ToLower() == request.Code.ToLower(), cancellationToken);

        if (codeExists)
        {
            throw new InvalidOperationException($"A PermissionType with code '{request.Code}' already exists in this application.");
        }

        entity.Code = request.Code;
        entity.Name = request.Name;
        entity.Description = request.Description;

        await _repository.UpdateAsync(entity, cancellationToken);

        return Unit.Value;
    }
}
