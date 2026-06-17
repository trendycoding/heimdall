using Heimdall.Application.Common.Interfaces;
using Heimdall.Domain.Entities;
using Heimdall.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Heimdall.Application.Permissions.Commands.UpdatePermission;

public sealed class UpdatePermissionCommandHandler : IRequestHandler<UpdatePermissionCommand, Unit>
{
    private readonly IRepository<Permission> _repository;
    private readonly IHeimdallDbContext _dbContext;

    public UpdatePermissionCommandHandler(
        IRepository<Permission> repository,
        IHeimdallDbContext dbContext)
    {
        _repository = repository;
        _dbContext = dbContext;
    }

    public async Task<Unit> Handle(UpdatePermissionCommand request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken);

        if (entity is null)
        {
            throw new InvalidOperationException($"Permission '{request.Id}' was not found.");
        }

        // Enforce uniqueness of PermissionCode within the application (excluding current entity)
        var codeExists = await _dbContext.Permissions
            .AnyAsync(p => p.ApplicationId == entity.ApplicationId
                        && p.Id != entity.Id
                        && p.PermissionCode == request.PermissionCode, cancellationToken);

        if (codeExists)
        {
            throw new InvalidOperationException($"A Permission with code '{request.PermissionCode}' already exists in this application.");
        }

        entity.PermissionCode = request.PermissionCode;
        entity.Name = request.Name;
        entity.Description = request.Description;

        await _repository.UpdateAsync(entity, cancellationToken);

        return Unit.Value;
    }
}
